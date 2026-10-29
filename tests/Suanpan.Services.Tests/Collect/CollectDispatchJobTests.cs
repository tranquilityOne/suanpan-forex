using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Data;
using Suanpan.Services.Collect.Jobs;
using Suanpan.Services.Collect.Runs;
using Suanpan.Services.Collect.Tasks;
using Suanpan.Services.Collect.Tasks.Models;
using Suanpan.Services.Tests.Fixtures;
using Suanpan.Shared.Messages.Collect;
using Xunit;

namespace Suanpan.Services.Tests.Collect;

/// <summary>
/// 调度分发（采集设计 §4.2，FR-002）：到点创建"定时"运行记录并下发、错过的时间点只补最近一次、重复执行不重复下发、
/// 下发失败的兜底。cron 的到点计算在 CronScheduleTests 中测试。分发会扫描全部启用的任务，断言只看本测试建的数据
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class CollectDispatchJobTests(PostgresFixture postgres)
{
    private static readonly DateTime Now = new(2026, 10, 27, 12, 0, 30, DateTimeKind.Utc);

    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(Now));

    private readonly RecordingEventBus _bus = new();

    /// <summary>
    /// Job 停机一小时后恢复，错过的多个时间点只补最近一次；记账时间丢失后重跑同一轮，唯一索引保证不重复创建、不重复下发
    /// </summary>
    [Fact]
    public async Task Dispatch_QueuesLatestDuePointOnce()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var source = await TestData.AddDataSourceAsync(db, Ct);
        var hourAgo = Now.AddHours(-1);
        var task = await TestData.AddCollectTaskAsync(db, Ct, source.Id, enabled: true, cron: "*/5 * * * *", lastDispatchedAt: hourAgo);
        var job = Create(db);

        await job.RunAsync(Ct);
        await db.CollectTasks.Where(t => t.Id == task.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.LastDispatchedAt, hourAgo), Ct);
        await job.RunAsync(Ct);

        var run = await db.CollectRuns.AsNoTracking().SingleAsync(r => r.TaskId == task.Id, Ct);
        Assert.Equal((CollectRunTrigger.Scheduled, CollectRunStatus.Queued, new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc)),
            (run.Trigger, run.Status, run.ScheduledFor));
        Assert.Equal([run.Id], _bus.Published.OfType<CollectRunCommand>().Select(c => c.RunId));
        Assert.Equal(Now, await DispatchedAtAsync(db, task.Id));
    }

    /// <summary>BR-155、FR-001 验收第 4 条：停用的任务、停用的数据源下的任务、没有 cron 的任务都不定时分发</summary>
    [Fact]
    public async Task Dispatch_SkipsDisabledTasksSourcesAndManualOnlyTasks()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var source = await TestData.AddDataSourceAsync(db, Ct);
        var disabledSource = await TestData.AddDataSourceAsync(db, Ct, enabled: false);
        var earlier = Now.AddHours(-1);
        var disabled = await TestData.AddCollectTaskAsync(db, Ct, source.Id, enabled: false, cron: "*/5 * * * *", lastDispatchedAt: earlier);
        var underDisabledSource = await TestData.AddCollectTaskAsync(db, Ct, disabledSource.Id, enabled: true, cron: "*/5 * * * *", lastDispatchedAt: earlier);
        var manualOnly = await TestData.AddCollectTaskAsync(db, Ct, source.Id, enabled: true, lastDispatchedAt: earlier);

        await Create(db).RunAsync(Ct);

        long[] ids = [disabled.Id, underDisabledSource.Id, manualOnly.Id];
        Assert.False(await db.CollectRuns.AnyAsync(r => ids.Contains(r.TaskId), Ct));
        Assert.Empty(_bus.Published);
    }

    /// <summary>FR-002 验收第 1 条：每小时整点的任务在 11:10 改成每小时 30 分，12:00 的分发按新规则补 11:30，不按旧规则下发 12:00</summary>
    [Fact]
    public async Task Dispatch_FollowsChangedCronFromNextPoint()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var source = await TestData.AddDataSourceAsync(db, Ct);
        var task = await TestData.AddCollectTaskAsync(
            db, Ct, source.Id, enabled: true, cron: "0 * * * *", lastDispatchedAt: new DateTime(2026, 10, 27, 11, 0, 0, DateTimeKind.Utc));
        var editedAt = new FixedTimeProvider(new DateTimeOffset(2026, 10, 27, 11, 10, 0, TimeSpan.Zero));
        await new CollectTaskService(db, new StubParserCatalog(), editedAt).UpdateAsync(task.Id, new CollectTaskInput
        {
            Name = task.Name,
            SourceId = source.Id,
            ParserKey = StubParserCatalog.Schedule.Key,
            Cron = "30 * * * *",
        }, Ct);

        await Create(db).RunAsync(Ct);

        var scheduled = await db.CollectRuns.AsNoTracking().Where(r => r.TaskId == task.Id).Select(r => r.ScheduledFor).SingleAsync(Ct);
        Assert.Equal(new DateTime(2026, 10, 27, 11, 30, 0, DateTimeKind.Utc), scheduled);
    }

    /// <summary>
    /// 采集设计 §4.2 下发失败的兜底：排队满 2 分钟的重新发布一次（12:00 这一轮处理 11:57 ~ 11:58 排队的）；
    /// 排队超过 10 分钟的记为失败（ERR-COL-008）；刚排队的、已经开始的不动
    /// </summary>
    [Fact]
    public async Task Dispatch_RepublishesThenFailsStaleQueuedRuns()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var source = await TestData.AddDataSourceAsync(db, Ct);
        var task = await TestData.AddCollectTaskAsync(db, Ct, source.Id);
        var recent = Run(task, CollectRunStatus.Queued, new DateTime(2026, 10, 27, 11, 59, 0, DateTimeKind.Utc));
        var waiting = Run(task, CollectRunStatus.Queued, new DateTime(2026, 10, 27, 11, 57, 20, DateTimeKind.Utc));
        var stale = Run(task, CollectRunStatus.Queued, new DateTime(2026, 10, 27, 11, 50, 0, DateTimeKind.Utc));
        var running = Run(task, CollectRunStatus.Running, new DateTime(2026, 10, 27, 11, 40, 0, DateTimeKind.Utc));
        var waitingReparse = Run(task, CollectRunStatus.Queued, new DateTime(2026, 10, 27, 11, 57, 30, DateTimeKind.Utc));
        waitingReparse.Trigger = CollectRunTrigger.Reparse;
        db.CollectRuns.AddRange(recent, waiting, stale, running, waitingReparse);
        await db.SaveChangesAsync(Ct);

        await Create(db).RunAsync(Ct);

        Assert.Equal([waiting.Id], _bus.Published.OfType<CollectRunCommand>().Select(c => c.RunId));
        // 重新解析的记录重新下发 ReparseCommand（采集设计 §5.6）
        Assert.Equal([waitingReparse.Id], _bus.Published.OfType<ReparseCommand>().Select(c => c.RunId));
        var statuses = await db.CollectRuns.AsNoTracking().Where(r => r.TaskId == task.Id)
            .ToDictionaryAsync(r => r.Id, r => (r.Status, r.ErrorCode), Ct);
        Assert.Equal((CollectRunStatus.Failed, CollectErrorCodes.DispatchFailed), statuses[stale.Id]);
        Assert.Equal((CollectRunStatus.Queued, null), statuses[waiting.Id]);
        Assert.Equal((CollectRunStatus.Queued, null), statuses[recent.Id]);
        Assert.Equal((CollectRunStatus.Running, null), statuses[running.Id]);
    }

    private CollectDispatchJob Create(SuanpanDbContext db) => new(
        db,
        new CollectRunService(db, new StubParserCatalog(), _bus, _clock, NullLogger<CollectRunService>.Instance),
        new StubDistributedLock(),
        _clock,
        NullLogger<CollectDispatchJob>.Instance);

    private static CollectRun Run(CollectTask task, CollectRunStatus status, DateTime queuedAt) => new()
    {
        TaskId = task.Id,
        SourceId = task.SourceId,
        Trigger = CollectRunTrigger.Manual,
        Status = status,
        QueuedAt = queuedAt,
    };

    private static Task<DateTime?> DispatchedAtAsync(SuanpanDbContext db, long taskId)
        => db.CollectTasks.AsNoTracking().Where(t => t.Id == taskId).Select(t => t.LastDispatchedAt).SingleAsync(Ct);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
