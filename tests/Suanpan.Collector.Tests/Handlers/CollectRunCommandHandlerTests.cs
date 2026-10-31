using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Suanpan.Collector.Tests.Fixtures;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Core.Ops.Enums;
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

    /// <summary>FR-002 验收第 2 条：手动触发后运行记录从"排队"变为"运行中"，结束后为成功；请求的 User-Agent 带项目名和配置的联系邮箱（BR-005）</summary>
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
        Assert.Equal($"SuanpanForexBot/1.0 (+mailto:{CollectorApp.ContactEmail})", Assert.Single(fixture.Http.Requests(host)).UserAgent);
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

        // 非 2xx 的响应也存为快照（采集设计 §4.3）
        Assert.Equal([(1, 500), (2, 500), (3, 500), (4, 500)], (await app.GetSnapshotsAsync(run.Id)).Select(s => ((int)s.Attempt, (int)s.HttpStatus)));
    }

    /// <summary>任务参数不符合适配器的要求：不发请求、不重试，运行记为失败</summary>
    [Fact]
    public async Task InvalidParameters_FailWithoutRequestOrRetry()
    {
        var app = await fixture.GetAppAsync();
        var host = fixture.Http.NewHost();
        var task = await app.AddTaskAsync(host, parameters: "{\"pages\": 0}");

        var run = await app.WaitUntilFinishedAsync(await app.TriggerAsync(task));

        Assert.Equal((CollectRunStatus.Failed, CollectErrorCodes.RequestFailed, (short)1), (run.Status, run.ErrorCode, run.AttemptCount));
        Assert.Empty(fixture.Http.Requests(host));
    }

    /// <summary>
    /// FR-004、硬性规则 14：每个响应先存为快照再解析，入库服务收到与响应一一对应的快照 id（BR-045 的前提）；
    /// 解析结果存进运行记录（FR-004 主流程第 2 步），入库服务报告的因锁定跳过的条数写进运行记录
    /// </summary>
    [Fact]
    public async Task Run_SavesSnapshotsAndPassesTheirIdsToIngestor()
    {
        var app = await fixture.GetAppAsync();
        var host = fixture.Http.NewHost((request, _) => Task.FromResult(StubHttp.Reply(
            HttpStatusCode.OK, request.RequestUri!.AbsolutePath.EndsWith("/2", StringComparison.Ordinal) ? FakeIngestor.LockedRecord : "ok")));
        var task = await app.AddTaskAsync(host, parameters: "{\"pages\": 2}");

        var runId = await app.TriggerAsync(task);
        var run = await app.WaitUntilFinishedAsync(runId);

        var snapshots = await app.GetSnapshotsAsync(runId);
        Assert.Equal((CollectRunStatus.Succeeded, 1, 1), (run.Status, run.InsertedCount, run.LockedCount));
        Assert.Equal([$"https://{host}/page/1", $"https://{host}/page/2"], snapshots.Select(s => s.Url));
        Assert.Equal(snapshots.Select(s => s.Id), app.Ingestor.Requests[runId].SnapshotIds);
        Assert.Equal(2, JsonDocument.Parse(run.ParseOutput!).RootElement.GetProperty("total").GetInt32());
    }

    /// <summary>ERR-PARSE-003：内容为空、或解析器判定为拦截页时，快照标记为无效，运行记为失败，不入库</summary>
    [Theory]
    [InlineData("")]
    [InlineData(FakeParser.BlockedPage)]
    public async Task InvalidContent_MarksSnapshotInvalidAndFails(string body)
    {
        var app = await fixture.GetAppAsync();
        var host = fixture.Http.NewHost((_, _) => Task.FromResult(StubHttp.Reply(HttpStatusCode.OK, body)));
        var task = await app.AddTaskAsync(host);

        var runId = await app.TriggerAsync(task);
        var run = await app.WaitUntilFinishedAsync(runId);

        Assert.Equal((CollectRunStatus.Failed, CollectErrorCodes.InvalidSnapshot), (run.Status, run.ErrorCode));
        Assert.False(Assert.Single(await app.GetSnapshotsAsync(runId)).IsValid);
        Assert.False(app.Ingestor.Requests.ContainsKey(runId));
    }

    /// <summary>ERR-COL-007：快照写入失败时运行记为失败、不重试、不解析，并发出"重要"告警</summary>
    [Fact]
    public async Task SnapshotWriteFailure_FailsRunAndRaisesAlert()
    {
        var app = await fixture.GetAppAsync();
        var host = fixture.Http.NewHost();
        var task = await app.AddTaskAsync(host);
        app.SnapshotFaults.FailSource(task.SourceId);
        string sourceName = "";
        await app.ExecuteAsync(async db => sourceName = await db.DataSources.Where(s => s.Id == task.SourceId).Select(s => s.Name).SingleAsync(Ct));

        var run = await app.WaitUntilFinishedAsync(await app.TriggerAsync(task));

        Assert.Equal((CollectRunStatus.Failed, CollectErrorCodes.SnapshotWriteFailed, (short)1, (string?)null),
            (run.Status, run.ErrorCode, run.AttemptCount, run.ParseOutput));
        await Eventually.GetAsync(
            () => Task.FromResult(app.Alerts.Raised.ToList()),
            alerts => alerts.Contains(new RaisedAlert(AlertLevel.Important, sourceName)),
            alerts => $"已有 {alerts.Count} 条告警");
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

    /// <summary>
    /// BR-003、ERR-COL-010：每日上限 2 次时，一次执行中的第 3 个请求不发出，运行记为失败、不重试；
    /// 用到 90% 和第一次被拦下时各告警一次（重要）
    /// </summary>
    [Fact]
    public async Task DailyLimit_StopsRequestsWithoutRetry()
    {
        var app = await fixture.GetAppAsync();
        var host = fixture.Http.NewHost();
        var task = await app.AddTaskAsync(host, parameters: "{\"pages\": 3}", dailyLimit: 2);
        var sourceName = string.Empty;
        await app.ExecuteAsync(async db => sourceName = (await db.DataSources.SingleAsync(s => s.Id == task.SourceId)).Name);

        var run = await app.WaitUntilFinishedAsync(await app.TriggerAsync(task));

        Assert.Equal((CollectRunStatus.Failed, CollectErrorCodes.DailyLimitExceeded, (short)1), (run.Status, run.ErrorCode, run.AttemptCount));
        Assert.Equal(2, fixture.Http.Requests(host).Count);
        Assert.Equal([AlertLevel.Important, AlertLevel.Important], app.Alerts.Raised.Where(a => a.Subject == sourceName).Select(a => a.Level));
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
