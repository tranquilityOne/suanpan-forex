using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Suanpan.Collector.Execution;
using Suanpan.Collector.Tests.Fixtures;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Xunit;

namespace Suanpan.Collector.Tests.Execution;

/// <summary>执行中断的回收（采集设计 §4.3，ERR-COL-009）。判断规则的各个分支在 Core 的 CollectRunStatusRuleTests 中测试</summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class StaleRunRecoveryTests(CollectorFixture fixture)
{
    /// <summary>等待重试、但计划的重试时间早已过去（重试消息丢失）的记为失败；重试时间还没到的不动</summary>
    [Fact]
    public async Task Recover_FailsRunsWhoseRetryWasLost()
    {
        var app = await fixture.GetAppAsync();
        var task = await app.AddTaskAsync(fixture.Http.NewHost());
        var lost = Running(task, nextAttemptAt: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var waiting = Running(task, nextAttemptAt: new DateTime(2999, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await app.ExecuteAsync(async db =>
        {
            db.CollectRuns.AddRange(lost, waiting);
            await db.SaveChangesAsync(Ct);
        });

        await app.Services.GetRequiredService<StaleRunRecovery>().RecoverAsync(Ct);

        var statuses = new Dictionary<long, (CollectRunStatus, string?)>();
        await app.ExecuteAsync(async db => statuses = await db.CollectRuns.AsNoTracking()
            .Where(r => r.TaskId == task.Id)
            .ToDictionaryAsync(r => r.Id, r => (r.Status, r.ErrorCode), Ct));
        Assert.Equal((CollectRunStatus.Failed, CollectErrorCodes.Interrupted), statuses[lost.Id]);
        Assert.Equal((CollectRunStatus.Running, null), statuses[waiting.Id]);
    }

    /// <summary>采集设计 §5.6：重新解析超过 5 分钟没有心跳的记为失败；心跳还新的不动</summary>
    [Fact]
    public async Task Recover_FailsReparseWithoutRecentHeartbeat()
    {
        var app = await fixture.GetAppAsync();
        var task = await app.AddTaskAsync(fixture.Http.NewHost());
        var silent = Reparsing(task, heartbeatAt: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var alive = Reparsing(task, heartbeatAt: new DateTime(2999, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await app.ExecuteAsync(async db =>
        {
            db.CollectRuns.AddRange(silent, alive);
            await db.SaveChangesAsync(Ct);
        });

        await app.Services.GetRequiredService<StaleRunRecovery>().RecoverAsync(Ct);

        var statuses = new Dictionary<long, (CollectRunStatus, string?)>();
        await app.ExecuteAsync(async db => statuses = await db.CollectRuns.AsNoTracking()
            .Where(r => r.TaskId == task.Id)
            .ToDictionaryAsync(r => r.Id, r => (r.Status, r.ErrorCode), Ct));
        Assert.Equal((CollectRunStatus.Failed, CollectErrorCodes.Interrupted), statuses[silent.Id]);
        Assert.Equal((CollectRunStatus.Running, null), statuses[alive.Id]);
    }

    private static CollectRun Reparsing(CollectTask task, DateTime heartbeatAt)
    {
        var startedAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return new CollectRun
        {
            TaskId = task.Id,
            SourceId = task.SourceId,
            Trigger = CollectRunTrigger.Reparse,
            Status = CollectRunStatus.Running,
            QueuedAt = startedAt,
            StartedAt = startedAt,
            HeartbeatAt = heartbeatAt,
            Reparse = new CollectRunReparse { ParserKey = task.ParserKey, SnapshotIds = [1] },
        };
    }

    private static CollectRun Running(CollectTask task, DateTime nextAttemptAt)
    {
        var startedAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return new CollectRun
        {
            TaskId = task.Id,
            SourceId = task.SourceId,
            Trigger = CollectRunTrigger.Manual,
            Status = CollectRunStatus.Running,
            QueuedAt = startedAt,
            StartedAt = startedAt,
            AttemptCount = 1,
            NextAttemptAt = nextAttemptAt,
            Attempts = [new CollectAttempt { No = 1, StartedAt = startedAt, HttpStatus = 503 }],
        };
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
