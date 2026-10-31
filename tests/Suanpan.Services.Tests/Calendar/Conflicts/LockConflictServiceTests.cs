using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Rules;
using Suanpan.Core.Reference.Enums;
using Suanpan.Data;
using Suanpan.Data.Interceptors;
using Suanpan.Services.Calendar.Conflicts;
using Suanpan.Services.Calendar.Conflicts.Models;
using Suanpan.Services.Reference.Indicators;
using Suanpan.Services.Tests.Fixtures;
using Suanpan.Shared.Messages.Calendar;
using Xunit;

namespace Suanpan.Services.Tests.Calendar.Conflicts;

/// <summary>
/// 锁定冲突的处理（日历设计 §3.9，FR-011）。能否采用、"保持锁定过"的判断在 Core 的 LockConflictRuleTests 中测试，
/// 这里测服务接上了规则以及数据库中的结果和发出的消息。断言只看本测试建的指标和事件
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class LockConflictServiceTests(PostgresFixture postgres)
{
    private static readonly DateTime Now = new(2026, 11, 6, 14, 0, 0, DateTimeKind.Utc);

    private readonly RecordingEventBus _bus = new();

    /// <summary>
    /// 保持锁定：row_version 不对时拒绝（ERR-EVT-009）；处理后冲突改为已处理，字段不变；已处理的不能再处理。
    /// 列表按页签分为待处理、已处理
    /// </summary>
    [Fact]
    public async Task Keep_ResolvesWithoutChangingTheField()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var source = await TestData.AddDataSourceAsync(db, Ct);
        var released = await AddLockedAsync(db, await AddIndicatorAsync(db), EventField.Actual);
        var conflict = await AddConflictAsync(db, released.Id, EventField.Actual, source.Id, "\"0.4\"", "\"0.3\"");
        var service = Create(db);

        var stale = await service.KeepAsync(conflict.Id, released.RowVersion - 1, Ct);
        var kept = await service.KeepAsync(conflict.Id, released.RowVersion, Ct);
        var again = await service.KeepAsync(conflict.Id, released.RowVersion, Ct);

        Assert.Equal((false, true, false), (stale.Success, kept.Success, again.Success));
        var stored = await db.LockConflicts.AsNoTracking().SingleAsync(c => c.Id == conflict.Id, Ct);
        Assert.Equal(
            (LockConflictStatus.Resolved, (LockConflictResolution?)LockConflictResolution.KeepLocked, "editor"),
            (stored.Status, stored.Resolution, stored.ResolvedBy));
        var calendarEvent = await db.CalendarEvents.AsNoTracking().SingleAsync(e => e.Id == released.Id, Ct);
        Assert.Equal(((decimal?)0.4m, true), (calendarEvent.Actual, EventLockRule.IsLocked(calendarEvent.LockedFields, EventField.Actual)));
        var pending = await service.QueryAsync(new LockConflictQuery(PageSize: 500), Ct);
        var resolved = await service.QueryAsync(new LockConflictQuery(Resolved: true, PageSize: 500), Ct);
        Assert.Equal((false, true), (pending.Items.Any(r => r.Conflict.Id == conflict.Id), resolved.Items.Any(r => r.Conflict.Id == conflict.Id)));
    }

    /// <summary>
    /// 解锁并采用公布值（FR-011 验收第 3 条）：移除锁定，写入采集值连同数据源、快照、检测时间，写"解锁"和"公布值"历史，
    /// 公布值填为下一期的前值；改的是公布过的事件，发出 EventValueEditedEvent（BR-052）
    /// </summary>
    [Fact]
    public async Task AdoptActual_UnlocksAndWritesCollectedValue()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var source = await TestData.AddDataSourceAsync(db, Ct);
        var code = await AddIndicatorAsync(db);
        var released = await AddLockedAsync(db, code, EventField.Actual);
        var next = TestData.Event(code, "2026-11");
        db.CalendarEvents.Add(next);
        db.CalendarEventValues.Add(new CalendarEventValue
        {
            EventId = released.Id, SourceId = source.Id, Value = 0.3m, Raw = "0.3", SnapshotId = 77, RunId = 1, DetectedAt = Now.AddMinutes(-5),
        });
        await db.SaveChangesAsync(Ct);
        var conflict = await AddConflictAsync(db, released.Id, EventField.Actual, source.Id, "\"0.4\"", "\"0.3\"");

        var result = await Create(db).AdoptAsync(conflict.Id, released.RowVersion, Ct);

        Assert.True(result.Success);
        var adopted = await db.CalendarEvents.AsNoTracking().SingleAsync(e => e.Id == released.Id, Ct);
        Assert.Equal(
            ((decimal?)0.3m, (long?)source.Id, (long?)77, (DateTime?)Now.AddMinutes(-5), 0),
            (adopted.Actual, adopted.ActualSourceId, adopted.SourceSnapshotId, adopted.DetectedAt, adopted.LockedFields.Count));
        Assert.Equal(0.3m, (await db.CalendarEvents.AsNoTracking().SingleAsync(e => e.Id == next.Id, Ct)).Previous);
        var changes = await db.CalendarEventChanges.AsNoTracking().Where(c => c.EventId == released.Id).OrderBy(c => c.Id).Select(c => c.Kind).ToListAsync(Ct);
        Assert.Equal([EventChangeKind.Unlocked, EventChangeKind.Actual], changes);
        Assert.Equal(LockConflictResolution.AdoptCollected, (await db.LockConflicts.AsNoTracking().SingleAsync(c => c.Id == conflict.Id, Ct)).Resolution);
        Assert.Equal([EventField.Actual], Assert.IsType<EventValueEditedEvent>(Assert.Single(_bus.Published)).Fields);
    }

    /// <summary>区间型指标的前值连同上限采用</summary>
    [Fact]
    public async Task AdoptRangePrevious_KeepsTheUpperBound()
    {
        await using var db = await postgres.CreateAppContextAsync();
        var source = await TestData.AddDataSourceAsync(db, Ct);
        var released = await AddLockedAsync(db, await AddIndicatorAsync(db, IndicatorValueType.Range), EventField.Previous);
        var conflict = await AddConflictAsync(db, released.Id, EventField.Previous, source.Id, "\"4.2\"", "\"4.5\"", collectedUpper: 4.7m);

        var result = await Create(db).AdoptAsync(conflict.Id, released.RowVersion, Ct);

        Assert.True(result.Success);
        var adopted = await db.CalendarEvents.AsNoTracking().SingleAsync(e => e.Id == released.Id, Ct);
        Assert.Equal(((decimal?)4.5m, (decimal?)4.7m, 0), (adopted.Previous, adopted.PreviousUpper, adopted.LockedFields.Count));
    }

    private LockConflictService Create(SuanpanDbContext db)
        => new(db, new FixedAuditActorProvider("editor"), _bus, new FixedTimeProvider(new DateTimeOffset(Now)), NullLogger<LockConflictService>.Instance);

    private static async Task<string> AddIndicatorAsync(SuanpanDbContext db, IndicatorValueType valueType = IndicatorValueType.Value)
    {
        var code = TestData.IndicatorCode();
        await new IndicatorService(db).CreateAsync(TestData.Indicator(code) with { ValueType = valueType }, Ct);
        return code;
    }

    /// <summary>已公布、公布值 0.4、前值 4.2 的事件，指定字段被人工锁定</summary>
    private static async Task<CalendarEvent> AddLockedAsync(SuanpanDbContext db, string code, EventField field)
    {
        var calendarEvent = TestData.Event(code);
        calendarEvent.Status = CalendarEventStatus.Released;
        calendarEvent.Actual = 0.4m;
        calendarEvent.Previous = 4.2m;
        calendarEvent.LockedFields = [new EventFieldLock { Field = field, LockedBy = "editor", LockedAt = Now.AddHours(-1), Reason = "人工录入" }];
        db.CalendarEvents.Add(calendarEvent);
        await db.SaveChangesAsync(Ct);
        return calendarEvent;
    }

    private static async Task<LockConflict> AddConflictAsync(
        SuanpanDbContext db, long eventId, EventField field, long sourceId, string lockedValue, string collectedValue, decimal? collectedUpper = null)
    {
        var conflict = new LockConflict
        {
            EventId = eventId, Field = field, LockedValue = lockedValue, CollectedValue = collectedValue, CollectedUpper = collectedUpper,
            SourceId = sourceId, SnapshotId = 88, RunId = 1, FirstDetectedAt = Now.AddMinutes(-10), LastDetectedAt = Now.AddMinutes(-5),
        };
        db.LockConflicts.Add(conflict);
        await db.SaveChangesAsync(Ct);
        return conflict;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
