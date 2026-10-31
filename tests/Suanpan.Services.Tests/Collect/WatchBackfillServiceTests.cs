using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Core.Reference.Entities;
using Suanpan.Core.Reference.Enums;
using Suanpan.Data;
using Suanpan.Services.Calendar.Events;
using Suanpan.Services.Collect.Runs;
using Suanpan.Services.Collect.Watch;
using Suanpan.Services.Reference.Indicators;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Collect;

/// <summary>
/// 延迟后的补采（BR-017，采集设计 §5.7）。间隔的分段在 Core 的 WatchRuleTests 中测试，这里测服务按任务合并、
/// 不在已有运行中的记录时重复下发、转为慢间隔时告警一次。断言只看本测试建的任务
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class WatchBackfillServiceTests(PostgresFixture postgres)
{
    private static readonly DateTime Now = new(2026, 11, 6, 15, 0, 0, DateTimeKind.Utc);

    private readonly RecordingEventBus _bus = new();
    private readonly RecordingAlertService _alerts = new();
    private readonly FakeRedisCache _cache = new();

    /// <summary>同一任务下两个延迟的事件只下发一条补采；它还在排队时不再下发</summary>
    [Fact]
    public async Task Run_EnqueuesOncePerTaskWhileNothingIsActive()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var task = await AddTaskAsync(db);
        await AddDelayedAsync(db, task, delayedAt: Now.AddHours(-1));
        await AddDelayedAsync(db, task, delayedAt: Now.AddMinutes(-20));
        var service = Create(db);

        var first = await service.RunAsync(BackfillSettings.Default, Ct);
        var second = await service.RunAsync(BackfillSettings.Default, Ct);

        var run = await db.CollectRuns.AsNoTracking().SingleAsync(r => r.TaskId == task.Id, Ct);
        Assert.Contains(run.Id, first);
        Assert.DoesNotContain(run.Id, second);
        Assert.Equal((CollectRunTrigger.Watch, CollectRunStatus.Queued, (DateTime?)null), (run.Trigger, run.Status, run.ScheduledFor));
    }

    /// <summary>BR-017：延迟超过 24 小时后改为每 60 分钟补采，并告警一次（重要，BR-129）</summary>
    [Fact]
    public async Task Run_SlowsDownAfterTheConfiguredHours()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var task = await AddTaskAsync(db);
        var code = await AddDelayedAsync(db, task, delayedAt: Now.AddHours(-25));
        db.CollectRuns.Add(FinishedRun(task, queuedAt: Now.AddMinutes(-30)));
        await db.SaveChangesAsync(Ct);

        var tooSoon = await Create(db).RunAsync(BackfillSettings.Default, Ct);
        var clock = Now.AddMinutes(31);
        var due = await Create(db, clock).RunAsync(BackfillSettings.Default, Ct);

        var runs = await db.CollectRuns.AsNoTracking().Where(r => r.TaskId == task.Id).OrderBy(r => r.Id).Select(r => r.Id).ToListAsync(Ct);
        Assert.Equal(2, runs.Count);
        Assert.Empty(tooSoon.Intersect(runs));
        Assert.Equal([runs[1]], due.Intersect(runs));
        Assert.Single(_alerts.Raised, a => a.Subject == code);
    }

    private WatchBackfillService Create(SuanpanDbContext db, DateTime? now = null)
    {
        var time = new FixedTimeProvider(new DateTimeOffset(now ?? Now));
        return new WatchBackfillService(db, new CollectRunService(db, new StubParserCatalog(), _bus, time, NullLogger<CollectRunService>.Instance),
            _cache, _alerts, time, NullLogger<WatchBackfillService>.Instance);
    }

    /// <summary>启用的数据源和它下面启用的公布值任务</summary>
    private static async Task<CollectTask> AddTaskAsync(SuanpanDbContext db)
    {
        var source = await TestData.AddDataSourceAsync(db, Ct);
        return await TestData.AddCollectTaskAsync(db, Ct, source.Id, CollectTargetType.Release, enabled: true);
    }

    /// <summary>映射到任务所在数据源的新指标，它的事件为延迟，状态历史记着变为延迟的时间</summary>
    private static async Task<string> AddDelayedAsync(SuanpanDbContext db, CollectTask task, DateTime delayedAt)
    {
        var code = TestData.IndicatorCode();
        await new IndicatorService(db).CreateAsync(TestData.Indicator(code) with { Importance = Importance.High }, Ct);
        db.IndicatorSources.Add(new IndicatorSource
        {
            IndicatorCode = code, SourceId = task.SourceId, Purpose = IndicatorSourcePurpose.Release, ExternalId = code, ParserKey = task.ParserKey,
            Priority = 1,
        });
        var calendarEvent = TestData.Event(code);
        calendarEvent.Status = CalendarEventStatus.Delayed;
        db.CalendarEvents.Add(calendarEvent);
        await db.SaveChangesAsync(Ct);
        db.CalendarEventChanges.Add(EventChanges.Create(calendarEvent.Id, EventChangeKind.Status, EventChanges.Status(CalendarEventStatus.Watching),
            EventChanges.Status(CalendarEventStatus.Delayed), new EventChangeActor(ChangeActorKind.Collector, "collector", delayedAt)));
        await db.SaveChangesAsync(Ct);
        return code;
    }

    private static CollectRun FinishedRun(CollectTask task, DateTime queuedAt) => new()
    {
        TaskId = task.Id, SourceId = task.SourceId, Trigger = CollectRunTrigger.Watch, Status = CollectRunStatus.Failed, QueuedAt = queuedAt,
        StartedAt = queuedAt, EndedAt = queuedAt.AddSeconds(5),
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
