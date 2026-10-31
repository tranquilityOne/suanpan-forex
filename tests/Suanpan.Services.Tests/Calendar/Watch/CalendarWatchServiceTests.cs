using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Ops.Enums;
using Suanpan.Core.Reference.Enums;
using Suanpan.Data;
using Suanpan.Data.Interceptors;
using Suanpan.Services.Calendar.Watch;
using Suanpan.Services.Reference.Indicators;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Calendar.Watch;

/// <summary>
/// 盯盘对事件状态的修改（采集设计 §5.7，日历设计 §3.2）。状态流转的规则在 Core 的 CalendarEventStatusRuleTests 中测试，
/// 这里测服务只改允许的事件、写带运行记录的状态历史、延迟时按重要性告警。断言只看本测试建的事件
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class CalendarWatchServiceTests(PostgresFixture postgres)
{
    private const long SourceId = 5;
    private const long RunId = 900;

    private static readonly DateTime Now = new(2026, 11, 6, 13, 29, 0, DateTimeKind.Utc);

    private readonly RecordingAlertService _alerts = new();

    /// <summary>待公布的改为盯盘中，写状态历史（采集、带数据源和运行记录）；已公布的跳过，已经是盯盘中的不重复改</summary>
    [Fact]
    public async Task Start_MovesScheduledEventsToWatching()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var code = await AddIndicatorAsync(db, Importance.High);
        var (scheduled, released, watching) = (Event(code, "2026-10"), Event(code, "2026-09", CalendarEventStatus.Released), Event(code, "2026-11", CalendarEventStatus.Watching));
        db.CalendarEvents.AddRange(scheduled, released, watching);
        await db.SaveChangesAsync(Ct);

        var changed = await Create(db).StartAsync([scheduled.Id, released.Id, watching.Id], SourceId, RunId, Ct);

        Assert.Equal([scheduled.Id], changed);
        var statuses = await StatusesAsync(db, code);
        Assert.Equal([CalendarEventStatus.Released, CalendarEventStatus.Watching, CalendarEventStatus.Watching], statuses.Select(s => s.Status));
        var change = await db.CalendarEventChanges.AsNoTracking().SingleAsync(c => c.EventId == scheduled.Id, Ct);
        Assert.Equal((EventChangeKind.Status, ChangeActorKind.Collector, (long?)SourceId, (long?)RunId), (change.Kind, change.ActorKind, change.SourceId, change.RunId));
    }

    /// <summary>ERR-EVT-001：只把盯盘中的改为延迟（待复核的不动），高重要性告警为紧急、中为重要（BR-129）</summary>
    [Fact]
    public async Task Delay_OnlyWatchingEventsAndAlertsByImportance()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var high = await AddIndicatorAsync(db, Importance.High);
        var medium = await AddIndicatorAsync(db, Importance.Medium);
        var (highEvent, mediumEvent, review) = (
            Event(high, "2026-10", CalendarEventStatus.Watching), Event(medium, "2026-10", CalendarEventStatus.Watching), Event(high, "2026-09", CalendarEventStatus.PendingReview));
        db.CalendarEvents.AddRange(highEvent, mediumEvent, review);
        await db.SaveChangesAsync(Ct);

        var changed = await Create(db).DelayAsync([highEvent.Id, mediumEvent.Id, review.Id], SourceId, RunId, Ct);

        Assert.Equal([highEvent.Id, mediumEvent.Id], changed.Order());
        Assert.Equal(CalendarEventStatus.PendingReview, (await db.CalendarEvents.AsNoTracking().SingleAsync(e => e.Id == review.Id, Ct)).Status);
        Assert.Equal([(AlertLevel.Urgent, high), (AlertLevel.Important, medium)], _alerts.Raised.OrderBy(a => a.Level));
    }

    /// <summary>盯盘期间官方改期：盯盘中的退回待公布（PRD v1.0.0.26）；已不是盯盘中的不改</summary>
    [Fact]
    public async Task ReturnToScheduled_OnlyFromWatching()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var code = await AddIndicatorAsync(db, Importance.High);
        var (watching, delayed) = (Event(code, "2026-10", CalendarEventStatus.Watching), Event(code, "2026-09", CalendarEventStatus.Delayed));
        db.CalendarEvents.AddRange(watching, delayed);
        await db.SaveChangesAsync(Ct);
        var service = Create(db);

        var returned = await service.ReturnToScheduledAsync(watching.Id, SourceId, RunId, Ct);
        var notWatching = await service.ReturnToScheduledAsync(delayed.Id, SourceId, RunId, Ct);

        Assert.Equal((true, false), (returned, notWatching));
        Assert.Equal([CalendarEventStatus.Delayed, CalendarEventStatus.Scheduled], (await StatusesAsync(db, code)).Select(s => s.Status));
    }

    private CalendarWatchService Create(SuanpanDbContext db)
        => new(db, new FixedAuditActorProvider("collector"), _alerts, new FixedTimeProvider(new DateTimeOffset(Now)), NullLogger<CalendarWatchService>.Instance);

    private static CalendarEvent Event(string code, string period, CalendarEventStatus status = CalendarEventStatus.Scheduled)
    {
        var calendarEvent = TestData.Event(code, period);
        calendarEvent.Status = status;
        return calendarEvent;
    }

    private static async Task<string> AddIndicatorAsync(SuanpanDbContext db, Importance importance)
    {
        var code = TestData.IndicatorCode();
        await new IndicatorService(db).CreateAsync(TestData.Indicator(code) with { Importance = importance }, Ct);
        return code;
    }

    /// <summary>按参考期排列</summary>
    private static Task<List<CalendarEvent>> StatusesAsync(SuanpanDbContext db, string code)
        => db.CalendarEvents.AsNoTracking().Where(e => e.IndicatorCode == code).OrderBy(e => e.ReferencePeriod).ToListAsync(Ct);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
