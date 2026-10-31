using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Reference.Enums;
using Suanpan.Data;
using Suanpan.Data.Interceptors;
using Suanpan.Services.Calendar.Events;
using Suanpan.Services.Calendar.Events.Models;
using Suanpan.Services.Reference.Indicators;
using Suanpan.Services.Tests.Fixtures;
using Xunit;

namespace Suanpan.Services.Tests.Calendar.Events;

/// <summary>
/// 待确认事件的保留、取消（BR-040，日历设计 §4.4、§9.1）。状态能否流转在 Core 的 CalendarEventStatusRuleTests 中测试，
/// 这里测服务接上了规则以及数据库中的结果。断言只看本测试建的指标
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class CalendarEventServiceTests(PostgresFixture postgres)
{
    private static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 列表显示维护排期的数据源和变为待确认的时间；保留后改回待公布、与数据源脱钩，变更历史记为人工，
    /// 并从待确认列表中消失
    /// </summary>
    [Fact]
    public async Task Keep_ReturnsToScheduledAndDetachesFromSource()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Create(db);
        var (code, calendarEvent, source) = await AddUnconfirmedAsync(db);

        var before = Assert.Single((await service.QueryUnconfirmedAsync(new UnconfirmedEventQuery(code), Ct)).Items);
        var result = await service.KeepAsync(calendarEvent.Id, calendarEvent.RowVersion, Ct);

        Assert.Equal((source.Name, (DateTime?)Now.AddHours(-1)), (before.ScheduleSourceName, before.UnconfirmedAt));
        Assert.True(result.Success);
        var kept = await db.CalendarEvents.AsNoTracking().SingleAsync(e => e.Id == calendarEvent.Id, Ct);
        Assert.Equal(
            (CalendarEventStatus.Scheduled, (long?)null, (string?)null, calendarEvent.RowVersion + 1),
            (kept.Status, kept.ScheduleSourceId, kept.ScheduleReleaseKey, kept.RowVersion));
        var change = await db.CalendarEventChanges.AsNoTracking().OrderBy(c => c.Id).LastAsync(c => c.EventId == calendarEvent.Id, Ct);
        Assert.Equal((ChangeActorKind.User, "editor", Now), (change.ActorKind, change.ChangedBy, change.ChangedAt));
        Assert.Empty((await service.QueryUnconfirmedAsync(new UnconfirmedEventQuery(code), Ct)).Items);
    }

    /// <summary>ERR-EVT-008：取消必须填原因，原因写进变更历史；ERR-EVT-010：已公布的事件不能取消</summary>
    [Fact]
    public async Task Cancel_RequiresReasonAndAllowedStatus()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Create(db);
        var (_, calendarEvent, _) = await AddUnconfirmedAsync(db);
        var released = TestData.Event(calendarEvent.IndicatorCode, "2026-09");
        released.Status = CalendarEventStatus.Released;
        db.CalendarEvents.Add(released);
        await db.SaveChangesAsync(Ct);

        var blank = await service.CancelAsync(calendarEvent.Id, "  ", calendarEvent.RowVersion, Ct);
        var notAllowed = await service.CancelAsync(released.Id, "官方停发", released.RowVersion, Ct);
        var cancelled = await service.CancelAsync(calendarEvent.Id, "官方停发", calendarEvent.RowVersion, Ct);

        Assert.Equal((false, "Reason"), (blank.Success, blank.Field));
        Assert.False(notAllowed.Success);
        Assert.True(cancelled.Success);
        Assert.Equal(CalendarEventStatus.Released, (await db.CalendarEvents.AsNoTracking().SingleAsync(e => e.Id == released.Id, Ct)).Status);
        Assert.Equal(CalendarEventStatus.Cancelled, (await db.CalendarEvents.AsNoTracking().SingleAsync(e => e.Id == calendarEvent.Id, Ct)).Status);
        var change = await db.CalendarEventChanges.AsNoTracking().OrderBy(c => c.Id).LastAsync(c => c.EventId == calendarEvent.Id, Ct);
        Assert.Equal((EventChangeKind.Status, "官方停发"), (change.Kind, change.Reason));
    }

    /// <summary>ERR-EVT-009：row_version 与读取时不同（事件被他人或采集改过）时拒绝，事件不变</summary>
    [Fact]
    public async Task KeepAndCancel_RejectStaleRowVersion()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Create(db);
        var (_, calendarEvent, _) = await AddUnconfirmedAsync(db);
        var stale = calendarEvent.RowVersion - 1;

        var keep = await service.KeepAsync(calendarEvent.Id, stale, Ct);
        var cancel = await service.CancelAsync(calendarEvent.Id, "官方停发", stale, Ct);

        Assert.Equal((false, false), (keep.Success, cancel.Success));
        var unchanged = await db.CalendarEvents.AsNoTracking().SingleAsync(e => e.Id == calendarEvent.Id, Ct);
        Assert.Equal((CalendarEventStatus.Unconfirmed, calendarEvent.RowVersion), (unchanged.Status, unchanged.RowVersion));
    }

    /// <summary>
    /// BR-048、FR-009 验收第 1 条：日期范围、经济体、重要性、状态、缺少预期值可以组合；按排期时间升序。
    /// 用一段没有别的数据的日期，断言只看本测试建的指标
    /// </summary>
    [Fact]
    public async Task Query_CombinesFilters()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Create(db);
        var (usHigh, usMedium, eaHigh) = (TestData.IndicatorCode(), TestData.IndicatorCode(), TestData.IndicatorCode("EA"));
        var indicators = new IndicatorService(db);
        await indicators.CreateAsync(TestData.Indicator(usHigh) with { Importance = Importance.High }, Ct);
        await indicators.CreateAsync(TestData.Indicator(usMedium), Ct);
        await indicators.CreateAsync(TestData.Indicator(eaHigh, "EA", "EUR") with { Importance = Importance.High }, Ct);
        var week = new DateTime(2031, 3, 3, 0, 0, 0, DateTimeKind.Utc);
        var later = At(TestData.Event(usHigh, "2031-02"), week.AddDays(2));
        var earlier = At(TestData.Event(usHigh, "2031-01"), week.AddDays(1));
        earlier.Forecast = 0.3m;
        var released = At(TestData.Event(usHigh, "2030-12"), week.AddDays(3));
        released.Status = CalendarEventStatus.Released;
        db.CalendarEvents.AddRange(
            later, earlier, released, At(TestData.Event(usMedium), week.AddDays(1)), At(TestData.Event(eaHigh), week.AddDays(1)),
            At(TestData.Event(usHigh, "2031-03"), week.AddDays(8)));
        await db.SaveChangesAsync(Ct);
        string[] mine = [usHigh, usMedium, eaHigh];

        async Task<List<long>> QueryAsync(EventQuery query)
            => (await service.QueryAsync(query, Ct)).Items.Where(r => mine.Contains(r.Event.IndicatorCode)).Select(r => r.Event.Id).ToList();

        var usHighThisWeek = await QueryAsync(new EventQuery(week, week.AddDays(7), "US", Importance.High));
        var scheduledWithoutForecast = await QueryAsync(new EventQuery(week, week.AddDays(7), "US", Importance.High, CalendarEventStatus.Scheduled, MissingForecast: true));

        Assert.Equal([earlier.Id, later.Id, released.Id], usHighThisWeek);
        Assert.Equal([later.Id], scheduledWithoutForecast);
    }

    /// <summary>
    /// FR-009 主流程第 2 步：详情带各数据源的数值、锁定冲突（待处理的在前）、变更历史（时间倒序）和其中数据源的名称
    /// </summary>
    [Fact]
    public async Task GetDetail_IncludesValuesConflictsAndChanges()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var service = Create(db);
        var (code, calendarEvent, source) = await AddUnconfirmedAsync(db);
        db.CalendarEventValues.Add(new CalendarEventValue
        {
            EventId = calendarEvent.Id, SourceId = source.Id, Value = 0.3m, Raw = "0.3", RunId = 1, DetectedAt = Now,
        });
        db.LockConflicts.AddRange(
            Conflict(calendarEvent.Id, source.Id, LockConflictStatus.Resolved, Now),
            Conflict(calendarEvent.Id, source.Id, LockConflictStatus.Pending, Now.AddHours(-2)));
        db.CalendarEventChanges.Add(EventChanges.Create(calendarEvent.Id, EventChangeKind.Actual, null, EventChanges.Number(0.3m),
            new EventChangeActor(ChangeActorKind.User, "editor", Now), "官方补发"));
        await db.SaveChangesAsync(Ct);

        var detail = await service.GetDetailAsync(calendarEvent.Id, Ct);

        Assert.Equal(code, detail!.Indicator.Code);
        Assert.Equal(0.3m, Assert.Single(detail.Values).Value);
        Assert.Equal([LockConflictStatus.Pending, LockConflictStatus.Resolved], detail.Conflicts.Select(c => c.Status));
        Assert.Equal([EventChangeKind.Actual, EventChangeKind.Status], detail.Changes.Select(c => c.Kind));
        Assert.Equal(source.Name, detail.SourceNames[source.Id]);
    }

    private static CalendarEvent At(CalendarEvent calendarEvent, DateTime scheduledAt)
    {
        calendarEvent.ScheduledAt = scheduledAt;
        return calendarEvent;
    }

    private static LockConflict Conflict(long eventId, long sourceId, LockConflictStatus status, DateTime lastDetectedAt) => new()
    {
        EventId = eventId, Field = EventField.Actual, LockedValue = EventChanges.Number(0.4m)!, CollectedValue = EventChanges.Number(0.3m)!,
        SourceId = sourceId, RunId = 1, FirstDetectedAt = lastDetectedAt, LastDetectedAt = lastDetectedAt, Status = status,
    };

    private static CalendarEventService Create(SuanpanDbContext db)
        => new(db, new FixedAuditActorProvider("editor"), new FixedTimeProvider(new DateTimeOffset(Now)));

    /// <summary>新指标的一个待确认事件：由新数据源的 empsit 维护，一小时前从官方日程中消失</summary>
    private static async Task<(string Code, CalendarEvent Event, DataSource Source)> AddUnconfirmedAsync(SuanpanDbContext db)
    {
        var code = TestData.IndicatorCode();
        await new IndicatorService(db).CreateAsync(TestData.Indicator(code), Ct);
        var source = await TestData.AddDataSourceAsync(db, Ct);
        var calendarEvent = TestData.Event(code);
        calendarEvent.Status = CalendarEventStatus.Unconfirmed;
        calendarEvent.ScheduleSourceId = source.Id;
        calendarEvent.ScheduleReleaseKey = "empsit";
        calendarEvent.RowVersion = 2;
        db.CalendarEvents.Add(calendarEvent);
        await db.SaveChangesAsync(Ct);
        db.CalendarEventChanges.Add(EventChanges.Create(calendarEvent.Id, EventChangeKind.Status,
            EventChanges.Status(CalendarEventStatus.Scheduled), EventChanges.Status(CalendarEventStatus.Unconfirmed),
            new EventChangeActor(ChangeActorKind.Collector, "collector", Now.AddHours(-1), source.Id), "从官方日程中消失"));
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();
        return (code, calendarEvent, source);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
