using System.Net;
using Microsoft.EntityFrameworkCore;
using Suanpan.Collector.Tests.Fixtures;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Xunit;

namespace Suanpan.Collector.Tests.Handlers;

/// <summary>
/// 重新解析的执行（采集设计 §5.6，FR-004 主流程第 3、4 步）：经 RabbitMQ 下发 ReparseCommand，Collector 读出选中的快照，
/// 按原来的请求分组还原后解析、入库。用测试的解析器（每个响应一条记录，内容为请求路径）和入库服务
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class ReparseCommandHandlerTests(CollectorFixture fixture)
{
    /// <summary>
    /// 两次运行各请求两页：选中的快照按原来的请求分组、组内按序号还原，每组入库一次，快照 id 与响应一一对应；
    /// 无效的快照跳过并计数，运行记为部分成功（ERR-PARSE-003）。同一任务有正在运行（等待重试）的采集时照常执行，不参与并发检查
    /// </summary>
    [Fact]
    public async Task Reparse_RestoresRequestGroupsAndSkipsInvalidSnapshots()
    {
        var app = await fixture.GetAppAsync();
        var host = fixture.Http.NewHost((request, _) => Task.FromResult(StubHttp.Reply(HttpStatusCode.OK, request.RequestUri!.AbsolutePath)));
        var task = await app.AddTaskAsync(host, parameters: "{\"pages\": 2}");
        var first = await app.GetSnapshotsAsync((await app.WaitUntilFinishedAsync(await app.TriggerAsync(task))).Id);
        var second = await app.GetSnapshotsAsync((await app.WaitUntilFinishedAsync(await app.TriggerAsync(task))).Id);
        await app.ExecuteAsync(async db =>
        {
            await db.Snapshots.Where(s => s.Id == second[0].Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsValid, false), Ct);
            db.CollectRuns.Add(WaitingRetry(task));
            await db.SaveChangesAsync(Ct);
        });

        var runId = await app.ReparseAsync(task, second[1].Id, first[1].Id, second[0].Id, first[0].Id);
        var run = await app.WaitUntilFinishedAsync(runId);

        Assert.Equal((CollectRunStatus.PartiallySucceeded, CollectErrorCodes.PartialData, 3), (run.Status, run.ErrorCode, run.InsertedCount));
        Assert.Contains(second[0].Id.ToString(), run.ErrorMessage);
        var requests = app.Ingestor.AllRequests[runId].ToList();
        Assert.Equal([$"{first[0].Id},{first[1].Id}", $"{second[1].Id}"], requests.Select(r => string.Join(",", r.SnapshotIds)));
        Assert.Equal(["/page/1", "/page/2"], requests[0].Records.Cast<string>());
    }

    /// <summary>选中的快照都无效时没有可解析的内容，运行记为失败（ERR-PARSE-003）</summary>
    [Fact]
    public async Task Reparse_AllInvalid_Fails()
    {
        var app = await fixture.GetAppAsync();
        var task = await app.AddTaskAsync(fixture.Http.NewHost((_, _) => Task.FromResult(StubHttp.Reply(HttpStatusCode.OK, FakeParser.BlockedPage))));
        var blocked = Assert.Single(await app.GetSnapshotsAsync((await app.WaitUntilFinishedAsync(await app.TriggerAsync(task))).Id));

        var run = await app.WaitUntilFinishedAsync(await app.ReparseAsync(task, blocked.Id));

        Assert.Equal((CollectRunStatus.Failed, CollectErrorCodes.InvalidSnapshot), (run.Status, run.ErrorCode));
    }

    /// <summary>同一任务的一条采集：第 1 次请求失败后等待重试（重试时间在很远的将来，不会被回收），状态为"运行中"</summary>
    private static CollectRun WaitingRetry(CollectTask task)
    {
        var startedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return new CollectRun
        {
            TaskId = task.Id,
            SourceId = task.SourceId,
            Trigger = CollectRunTrigger.Manual,
            Status = CollectRunStatus.Running,
            QueuedAt = startedAt,
            StartedAt = startedAt,
            AttemptCount = 1,
            NextAttemptAt = new DateTime(2999, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Attempts = [new CollectAttempt { No = 1, StartedAt = startedAt, HttpStatus = 503 }],
        };
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
