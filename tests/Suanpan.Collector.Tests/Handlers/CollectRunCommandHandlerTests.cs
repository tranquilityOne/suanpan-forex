using System.Net;
using Microsoft.EntityFrameworkCore;
using Suanpan.Collector.Tests.Fixtures;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Xunit;

namespace Suanpan.Collector.Tests.Handlers;

/// <summary>
/// Collector 执行框架的端到端测试（采集设计 §4.3、§12）：经 RabbitMQ 下发 CollectRunCommand，Collector 在后台执行，
/// 测试轮询运行记录的状态。数据源指向 StubHttp，用测试的适配器、解析器和入库服务；重试间隔为 0 秒
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class CollectRunCommandHandlerTests(CollectorFixture fixture)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>FR-002 验收第 2 条：手动触发后运行记录从"排队"变为"运行中"，结束后为成功；请求带项目的 User-Agent（BR-005）</summary>
    [Fact]
    public async Task ManualRun_GoesRunningThenSucceeds()
    {
        var app = await fixture.GetAppAsync();
        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var host = fixture.Http.NewHost(async (_, ct) =>
        {
            arrived.TrySetResult();
            await release.Task.WaitAsync(ct);
            return StubHttp.Reply(HttpStatusCode.OK);
        });
        var task = await app.AddTaskAsync(host);

        var runId = await app.TriggerAsync(task);
        await arrived.Task.WaitAsync(Timeout, Ct);
        var running = await app.GetRunAsync(runId);
        release.SetResult();
        var finished = await app.WaitUntilFinishedAsync(runId);

        Assert.Equal((CollectRunStatus.Running, (short)1), (running.Status, running.AttemptCount));
        Assert.Equal((CollectRunStatus.Succeeded, 1), (finished.Status, finished.InsertedCount));
        Assert.Equal(200, Assert.Single(finished.Attempts).HttpStatus);
        Assert.StartsWith("SuanpanForexBot/", Assert.Single(fixture.Http.Requests(host)).UserAgent);
    }

    /// <summary>FR-002 验收第 3 条、BR-009：一直返回 500 时请求 4 次（第 1 次 + 重试 3 次）后记为失败，每次请求都有记录</summary>
    [Fact]
    public async Task FailingRequests_RetryThreeTimesThenFail()
    {
        var app = await fixture.GetAppAsync();
        var host = fixture.Http.NewHost((_, _) => Task.FromResult(StubHttp.Reply(HttpStatusCode.InternalServerError)));
        var task = await app.AddTaskAsync(host);

        var run = await app.WaitUntilFinishedAsync(await app.TriggerAsync(task));

        Assert.Equal((CollectRunStatus.Failed, CollectErrorCodes.RequestFailed, (short)4), (run.Status, run.ErrorCode, run.AttemptCount));
        Assert.Equal([(1, 500), (2, 500), (3, 500), (4, 500)], run.Attempts.Select(a => ((int)a.No, a.HttpStatus ?? 0)));
        Assert.Equal(4, fixture.Http.Requests(host).Count);
    }

    /// <summary>
    /// FR-002 验收第 4 条、BR-010：上一次运行还没结束（第 1 次请求失败后等待重试，仍是"运行中"）时，
    /// 同一任务的新运行记为跳过（ERR-COL-004）；上一次照常重试并成功
    /// </summary>
    [Fact]
    public async Task SameTask_NewRunSkippedWhilePreviousIsRunning()
    {
        var app = await fixture.GetAppAsync();
        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var host = fixture.Http.NewHost(async (_, ct) =>
        {
            if (Interlocked.Increment(ref calls) > 1)
            {
                return StubHttp.Reply(HttpStatusCode.OK);
            }

            arrived.TrySetResult();
            await release.Task.WaitAsync(ct);
            return StubHttp.Reply(HttpStatusCode.ServiceUnavailable);
        });
        var task = await app.AddTaskAsync(host);

        var first = await app.TriggerAsync(task);
        await arrived.Task.WaitAsync(Timeout, Ct);
        var second = await app.TriggerAsync(task);
        release.SetResult();

        var skipped = await app.WaitUntilFinishedAsync(second);
        var succeeded = await app.WaitUntilFinishedAsync(first);
        Assert.Equal((CollectRunStatus.Skipped, CollectErrorCodes.ConcurrentRun), (skipped.Status, skipped.ErrorCode));
        Assert.Equal((CollectRunStatus.Succeeded, (short)2), (succeeded.Status, succeeded.AttemptCount));
    }

    /// <summary>FR-001 验收第 4 条：第 1 次请求失败后数据源被停用，重试前检查到停用就记为跳过，不再发请求</summary>
    [Fact]
    public async Task SourceDisabledBeforeRetry_SendsNoMoreRequests()
    {
        var app = await fixture.GetAppAsync();
        CollectTask? task = null;
        var host = fixture.Http.NewHost(async (_, _) =>
        {
            await app.ExecuteAsync(db => db.DataSources.Where(s => s.Id == task!.SourceId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsEnabled, false), Ct));
            return StubHttp.Reply(HttpStatusCode.ServiceUnavailable);
        });
        task = await app.AddTaskAsync(host);

        var run = await app.WaitUntilFinishedAsync(await app.TriggerAsync(task));

        Assert.Equal((CollectRunStatus.Skipped, (short)1), (run.Status, run.AttemptCount));
        Assert.Single(fixture.Http.Requests(host));
    }

    /// <summary>BR-003：上限 1 次/秒时，一次执行中的 3 个请求至少有一个要等下一个窗口，请求记录写入延后的时长（ERR-COL-003）</summary>
    [Fact]
    public async Task RateLimit_DefersRequestsBeyondTheLimit()
    {
        var app = await fixture.GetAppAsync();
        var host = fixture.Http.NewHost();
        var task = await app.AddTaskAsync(host, rateLimit: 1, parameters: "{\"pages\": 3}");

        var run = await app.WaitUntilFinishedAsync(await app.TriggerAsync(task));

        Assert.Equal((CollectRunStatus.Succeeded, 3), (run.Status, run.InsertedCount));
        Assert.True(Assert.Single(run.Attempts).DeferredMs > 0);
        Assert.Equal(3, fixture.Http.Requests(host).Count);
    }

    /// <summary>
    /// 消费端幂等：同一条指令投递两次只执行一次。Collector 按到达顺序处理消息，
    /// 等排在后面的另一条运行结束，就说明重复的那条已经处理过
    /// </summary>
    [Fact]
    public async Task DuplicateCommand_ExecutesOnce()
    {
        var app = await fixture.GetAppAsync();
        var host = fixture.Http.NewHost();
        var task = await app.AddTaskAsync(host);
        var markerTask = await app.AddTaskAsync(fixture.Http.NewHost());

        var runId = await app.TriggerAsync(task);
        await app.PublishAsync(runId);
        await app.WaitUntilFinishedAsync(await app.TriggerAsync(markerTask));

        Assert.Equal(CollectRunStatus.Succeeded, (await app.GetRunAsync(runId)).Status);
        Assert.Single(fixture.Http.Requests(host));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
