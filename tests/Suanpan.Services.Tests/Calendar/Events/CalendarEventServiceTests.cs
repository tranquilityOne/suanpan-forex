using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Collect.Entities;
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
