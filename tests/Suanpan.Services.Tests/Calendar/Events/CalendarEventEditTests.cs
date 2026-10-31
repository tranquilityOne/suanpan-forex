using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Rules;
using Suanpan.Data;
using Suanpan.Data.Interceptors;
using Suanpan.Services.Calendar.Events;
using Suanpan.Services.Calendar.Events.Models;
using Suanpan.Services.Reference.Indicators;
using Suanpan.Services.Tests.Fixtures;
using Suanpan.Shared.Messages.Calendar;
using Xunit;

namespace Suanpan.Services.Tests.Calendar.Events;

/// <summary>
/// 事件的人工修改、待复核的处理、解锁（日历设计 §3.7、§3.9，BR-050 ~ BR-052、BR-063）。各操作在什么状态下允许、数值格式的分支
/// 在 Core 的 EventEditRuleTests、EventValueRuleTests 中测试，这里测服务接上了规则以及数据库中的结果和发出的消息。
/// 指标为数值型、百分比、1 位小数；断言只看本测试建的指标
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class CalendarEventEditTests(PostgresFixture postgres)
{
    private static readonly DateTime Now = new(2026, 11, 6, 14, 0, 0, DateTimeKind.Utc);

    private readonly RecordingEventBus _bus = new();

    /// <summary>
    /// FR-009 验收第 2 条、BR-050：录入公布值后事件改为已公布、公布值被锁定（锁定人、原因），变更历史记为人工；
    /// 公布值填为下一期的前值，发出 EventReleasedEvent；没公布过的事件不发 EventValueEditedEvent
    /// </summary>
    [Fact]
    public async Task EditActual_ReleasesLocksAndFillsNextPrevious()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var code = await AddIndicatorAsync(db);
        var (october, november) = (TestData.Event(code), TestData.Event(code, "2026-11"));
        db.CalendarEvents.AddRange(october, november);
        await db.SaveChangesAsync(Ct);

        var result = await Create(db).EditValuesAsync(october.Id, new EventValuesInput(Actual: 0.4m), "官方 API 故障，按新闻稿录入", october.RowVersion, Ct);

        Assert.True(result.Success);
        var saved = await db.CalendarEvents.AsNoTracking().SingleAsync(e => e.Id == october.Id, Ct);
        Assert.Equal((CalendarEventStatus.Released, (decimal?)0.4m, (long?)null), (saved.Status, saved.Actual, saved.ActualSourceId));
        var fieldLock = Assert.Single(saved.LockedFields);
        Assert.Equal((EventField.Actual, "editor", "官方 API 故障，按新闻稿录入"), (fieldLock.Field, fieldLock.LockedBy, fieldLock.Reason));
        Assert.Equal(0.4m, (await db.CalendarEvents.AsNoTracking().SingleAsync(e => e.Id == november.Id, Ct)).Previous);
        var changes = await db.CalendarEventChanges.AsNoTracking().Where(c => c.EventId == october.Id).OrderBy(c => c.Id).ToListAsync(Ct);
        Assert.Equal([(EventChangeKind.Actual, ChangeActorKind.User), (EventChangeKind.Status, ChangeActorKind.User)], changes.Select(c => (c.Kind, c.ActorKind)));
        Assert.Equal(october.Id, Assert.IsType<EventReleasedEvent>(Assert.Single(_bus.Published)).EventId);
    }

    /// <summary>
    /// ERR-EVT-008、ERR-FC-001、ERR-EVT-009：原因必填、格式按指标、row_version 不对时拒绝；
    /// BR-052：改了已公布事件的前值，锁定前值并发出 EventValueEditedEvent，状态不变
    /// </summary>
    [Fact]
    public async Task EditPublished_ChecksReasonFormatAndVersion_ThenSendsEditedEvent()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var released = Released(TestData.Event(await AddIndicatorAsync(db)), 0.3m);
        released.Previous = 0.2m;
        db.CalendarEvents.Add(released);
        await db.SaveChangesAsync(Ct);
        var service = Create(db);

        var noReason = await service.EditValuesAsync(released.Id, new EventValuesInput(Previous: 0.1m), " ", released.RowVersion, Ct);
        var badFormat = await service.EditValuesAsync(released.Id, new EventValuesInput(Previous: 0.15m), "官方修订", released.RowVersion, Ct);
        var stale = await service.EditValuesAsync(released.Id, new EventValuesInput(Previous: 0.1m), "官方修订", released.RowVersion - 1, Ct);
        var saved = await service.EditValuesAsync(released.Id, new EventValuesInput(Previous: 0.1m), "官方修订", released.RowVersion, Ct);

        Assert.Equal(("Reason", "Previous", false, true), (noReason.Field, badFormat.Field, stale.Success, saved.Success));
        var edited = await db.CalendarEvents.AsNoTracking().SingleAsync(e => e.Id == released.Id, Ct);
        Assert.Equal((CalendarEventStatus.Released, (decimal?)0.1m, true), (edited.Status, edited.Previous, EventLockRule.IsLocked(edited.LockedFields, EventField.Previous)));
        Assert.Equal([EventField.Previous], Assert.IsType<EventValueEditedEvent>(Assert.Single(_bus.Published)).Fields);
    }

    /// <summary>
    /// BR-051：确认采集值取所选数据源的值、快照和检测时间，不锁定；驳回只适用于从未公布过的事件，改为延迟并记下原因；
    /// 官方更正的事件采用新值后发出 EventValueEditedEvent
    /// </summary>
    [Fact]
    public async Task Review_ConfirmAndReject()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var code = await AddIndicatorAsync(db);
        var source = await TestData.AddDataSourceAsync(db, Ct);
        var fresh = Review(TestData.Event(code, "2026-08"));
        var rejected = Review(TestData.Event(code, "2026-09"));
        var corrected = Review(Released(TestData.Event(code), 0.3m));
        db.CalendarEvents.AddRange(fresh, rejected, corrected);
        await db.SaveChangesAsync(Ct);
        db.CalendarEventValues.AddRange(Candidate(fresh.Id, source.Id, 0.5m), Candidate(corrected.Id, source.Id, 0.6m));
        await db.SaveChangesAsync(Ct);
        var service = Create(db);

        var rejectCorrected = await service.RejectReviewAsync(corrected.Id, "数值可疑", corrected.RowVersion, Ct);
        var reject = await service.RejectReviewAsync(rejected.Id, "解析器取错了序列", rejected.RowVersion, Ct);
        var confirmFresh = await service.ConfirmReviewAsync(fresh.Id, source.Id, fresh.RowVersion, Ct);
        var confirmCorrected = await service.ConfirmReviewAsync(corrected.Id, source.Id, corrected.RowVersion, Ct);

        Assert.Equal((false, true, true, true), (rejectCorrected.Success, reject.Success, confirmFresh.Success, confirmCorrected.Success));
        var events = await db.CalendarEvents.AsNoTracking().Where(e => e.IndicatorCode == code).ToDictionaryAsync(e => e.Id, Ct);
        // 确认 2026-08 后，它的公布值填为下一个未公布事件（驳回后为延迟的 2026-09）的前值
        Assert.Equal((CalendarEventStatus.Delayed, (decimal?)0.5m), (events[rejected.Id].Status, events[rejected.Id].Previous));
        var confirmed = events[fresh.Id];
        Assert.Equal(
            (CalendarEventStatus.Released, (decimal?)0.5m, (long?)source.Id, (long?)77, (DateTime?)Now.AddMinutes(-5), 0),
            (confirmed.Status, confirmed.Actual, confirmed.ActualSourceId, confirmed.SourceSnapshotId, confirmed.DetectedAt, confirmed.LockedFields.Count));
        Assert.Equal(0.6m, events[corrected.Id].Actual);
        var reason = await db.CalendarEventChanges.AsNoTracking()
            .Where(c => c.EventId == rejected.Id && c.Kind == EventChangeKind.Status).Select(c => c.Reason).SingleAsync(Ct);
        Assert.Equal("解析器取错了序列", reason);
        Assert.Equal([corrected.Id], _bus.Published.OfType<EventValueEditedEvent>().Select(m => m.EventId));
        Assert.Equal(2, _bus.Published.OfType<EventReleasedEvent>().Count());
    }

    /// <summary>BR-063：解锁后字段不再锁定，写一条解锁历史；这个字段待处理的冲突关闭，处理方式"已解锁"</summary>
    [Fact]
    public async Task Unlock_ClosesPendingConflicts()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var source = await TestData.AddDataSourceAsync(db, Ct);
        var released = Released(TestData.Event(await AddIndicatorAsync(db)), 0.4m);
        released.LockedFields = [new EventFieldLock { Field = EventField.Actual, LockedBy = "editor", LockedAt = Now, Reason = "人工录入" }];
        db.CalendarEvents.Add(released);
        await db.SaveChangesAsync(Ct);
        db.LockConflicts.Add(new LockConflict
        {
            EventId = released.Id, Field = EventField.Actual, LockedValue = "\"0.4\"", CollectedValue = "\"0.3\"", SourceId = source.Id, RunId = 1,
            FirstDetectedAt = Now, LastDetectedAt = Now,
        });
        await db.SaveChangesAsync(Ct);

        var result = await Create(db).UnlockAsync(released.Id, EventField.Actual, released.RowVersion, Ct);

        Assert.True(result.Success);
        Assert.Empty((await db.CalendarEvents.AsNoTracking().SingleAsync(e => e.Id == released.Id, Ct)).LockedFields);
        var conflict = await db.LockConflicts.AsNoTracking().SingleAsync(c => c.EventId == released.Id, Ct);
        Assert.Equal((LockConflictStatus.Resolved, (LockConflictResolution?)LockConflictResolution.Unlocked), (conflict.Status, conflict.Resolution));
        Assert.Equal(EventChangeKind.Unlocked, (await db.CalendarEventChanges.AsNoTracking().SingleAsync(c => c.EventId == released.Id, Ct)).Kind);
    }

    private CalendarEventService Create(SuanpanDbContext db)
        => new(db, new FixedAuditActorProvider("editor"), _bus, new FixedTimeProvider(new DateTimeOffset(Now)), NullLogger<CalendarEventService>.Instance);

    private static async Task<string> AddIndicatorAsync(SuanpanDbContext db)
    {
        var code = TestData.IndicatorCode();
        await new IndicatorService(db).CreateAsync(TestData.Indicator(code), Ct);
        return code;
    }

    private static CalendarEvent Released(CalendarEvent calendarEvent, decimal actual)
    {
        calendarEvent.Status = CalendarEventStatus.Released;
        calendarEvent.Actual = actual;
        return calendarEvent;
    }

    private static CalendarEvent Review(CalendarEvent calendarEvent)
    {
        calendarEvent.Status = CalendarEventStatus.PendingReview;
        return calendarEvent;
    }

    private static CalendarEventValue Candidate(long eventId, long sourceId, decimal value) => new()
    {
        EventId = eventId, SourceId = sourceId, Value = value, Raw = value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        SnapshotId = 77, RunId = 1, DetectedAt = Now.AddMinutes(-5),
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
