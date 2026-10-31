using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Rules;
using Suanpan.Data;
using Suanpan.Data.Interceptors;
using Suanpan.Infrastructure.Messaging;
using Suanpan.Services.Calendar.Conflicts.Models;
using Suanpan.Services.Calendar.Events;
using Suanpan.Services.Common.Querying;
using Suanpan.Shared.Messages.Calendar;
using Suanpan.Shared.Mvc;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Calendar.Conflicts;

public sealed class LockConflictService(
    SuanpanDbContext db,
    IAuditActorProvider auditActor,
    IEventBus eventBus,
    TimeProvider timeProvider,
    ILogger<LockConflictService> logger) : ILockConflictService
{
    private const string StaleError = "事件已被修改，请刷新页面后重试";

    /// <summary>解锁并采用时写进变更历史的原因</summary>
    private const string AdoptReason = "解锁并采用采集值";

    public Task<PagedResult<LockConflictRow>> QueryAsync(LockConflictQuery query, CancellationToken ct = default)
    {
        var status = query.Resolved ? LockConflictStatus.Resolved : LockConflictStatus.Pending;
        var rows = from c in db.LockConflicts.AsNoTracking()
                   where c.Status == status
                   join e in db.CalendarEvents on c.EventId equals e.Id
                   join i in db.Indicators on e.IndicatorCode equals i.Code
                   join s in db.DataSources on c.SourceId equals s.Id into sources
                   from s in sources.DefaultIfEmpty()
                   select new { Conflict = c, Event = e, Indicator = i, SourceName = s == null ? null : s.Name };
        var ordered = query.Resolved
            ? rows.OrderByDescending(x => x.Conflict.ResolvedAt).ThenByDescending(x => x.Conflict.Id)
            : rows.OrderByDescending(x => x.Conflict.LastDetectedAt).ThenByDescending(x => x.Conflict.Id);
        return ordered
            .Select(x => new LockConflictRow(
                x.Conflict, x.Event, x.Indicator.ShortName, x.Indicator.ValueType, x.Indicator.Unit, x.Indicator.Decimals, x.SourceName))
            .ToPagedResultAsync(query.Page, query.PageSize, ct);
    }

    public async Task<ManageResult> KeepAsync(long id, int rowVersion, CancellationToken ct = default)
    {
        var (conflict, _, error) = await FindAsync(id, rowVersion, ct);
        if (conflict is null)
        {
            return error!;
        }

        Resolve(conflict, LockConflictResolution.KeepLocked, UserActor());
        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    public async Task<ManageResult> AdoptAsync(long id, int rowVersion, CancellationToken ct = default)
    {
        var (conflict, calendarEvent, error) = await FindAsync(id, rowVersion, ct);
        if (conflict is null || calendarEvent is null)
        {
            return error!;
        }

        var field = conflict.Field;
        var collected = EventChanges.ParseNumber(conflict.CollectedValue);
        if (!LockConflictRule.CanAdopt(field) || (collected is null && field != EventField.PreviousRevised))
        {
            return ManageResult.Fail($"{CalendarDisplayNames.Get(field)}的冲突不能直接采用采集值，请在事件详情中解锁");
        }

        var actor = UserActor();
        var wasPublished = EventEditRule.WasPublished(calendarEvent);
        var locks = calendarEvent.LockedFields.ToList();
        EventLockRule.Unlock(locks, field);

        // 赋一个新的列表，jsonb 列的修改才会被检测到
        calendarEvent.LockedFields = locks;
        db.CalendarEventChanges.Add(EventChanges.Create(calendarEvent.Id, EventChangeKind.Unlocked, EventChanges.Field(field), null, actor, AdoptReason));
        switch (field)
        {
            case EventField.Actual:
                await AdoptActualAsync(calendarEvent, conflict, collected!.Value, actor, ct);
                break;
            case EventField.Previous:
                db.CalendarEventChanges.Add(EventChanges.Create(calendarEvent.Id, EventChangeKind.Previous,
                    EventChanges.Number(calendarEvent.Previous), EventChanges.Number(collected), actor, AdoptReason));
                calendarEvent.Previous = collected;
                calendarEvent.PreviousUpper = conflict.CollectedUpper;
                break;
            default:
                db.CalendarEventChanges.Add(EventChanges.Create(calendarEvent.Id, EventChangeKind.PreviousRevised,
                    EventChanges.Number(calendarEvent.PreviousRevised), EventChanges.Number(collected), actor, AdoptReason));
                calendarEvent.PreviousRevised = collected;
                break;
        }

        Resolve(conflict, LockConflictResolution.AdoptCollected, actor);
        calendarEvent.RowVersion++;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return ManageResult.Fail(StaleError);
        }

        if (wasPublished)
        {
            try
            {
                await eventBus.PublishAsync(
                    new EventValueEditedEvent(calendarEvent.Id, calendarEvent.IndicatorCode, calendarEvent.ReferencePeriod, [field], actor.ChangedBy), ct: ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "消息 {MessageName} 发布失败", nameof(EventValueEditedEvent));
            }
        }

        return ManageResult.Ok;
    }

    /// <summary>
    /// 公布值连同来源：这个数据源当前的数值与采集值相同时取它的原始字符串、快照和检测时间，否则取冲突记下的快照和最近检测时间；
    /// 与人工录入相同，把公布值填为下一期的前值
    /// </summary>
    private async Task AdoptActualAsync(CalendarEvent calendarEvent, LockConflict conflict, decimal value, EventChangeActor actor, CancellationToken ct)
    {
        var candidate = await db.CalendarEventValues.AsNoTracking()
            .FirstOrDefaultAsync(v => v.EventId == calendarEvent.Id && v.SourceId == conflict.SourceId, ct);
        if (candidate is not null && (candidate.Value != value || candidate.ValueUpper != conflict.CollectedUpper))
        {
            candidate = null;
        }

        db.CalendarEventChanges.Add(EventChanges.Create(calendarEvent.Id, EventChangeKind.Actual,
            EventChanges.Number(calendarEvent.Actual), EventChanges.Number(value), actor, AdoptReason));
        calendarEvent.Actual = value;
        calendarEvent.ActualUpper = conflict.CollectedUpper;
        calendarEvent.ActualRaw = candidate?.Raw;
        calendarEvent.ActualSourceId = conflict.SourceId;
        calendarEvent.SourceSnapshotId = candidate is null ? conflict.SnapshotId : candidate.SnapshotId;
        calendarEvent.DetectedAt = candidate?.DetectedAt ?? conflict.LastDetectedAt;
        await NextEventPrevious.FillAsync(db, calendarEvent, value, conflict.CollectedUpper, actor, ct);
    }

    private static void Resolve(LockConflict conflict, LockConflictResolution resolution, EventChangeActor actor)
    {
        conflict.Status = LockConflictStatus.Resolved;
        conflict.Resolution = resolution;
        conflict.ResolvedBy = actor.ChangedBy;
        conflict.ResolvedAt = actor.ChangedAt;
    }

    private EventChangeActor UserActor() => new(ChangeActorKind.User, auditActor.GetActor(), timeProvider.GetUtcNow().UtcDateTime);

    /// <summary>读出待处理的冲突和所属事件；冲突不存在、已处理，或事件的 row_version 与读取时不同（ERR-EVT-009）时返回错误</summary>
    private async Task<(LockConflict? Conflict, CalendarEvent? Event, ManageResult? Error)> FindAsync(long id, int rowVersion, CancellationToken ct)
    {
        var conflict = await db.LockConflicts.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (conflict is null)
        {
            return (null, null, ManageResult.Fail("冲突不存在"));
        }

        if (conflict.Status != LockConflictStatus.Pending)
        {
            return (null, null, ManageResult.Fail("冲突已经处理过了，请刷新页面"));
        }

        var calendarEvent = await db.CalendarEvents.FirstOrDefaultAsync(e => e.Id == conflict.EventId, ct);
        return calendarEvent?.RowVersion == rowVersion ? (conflict, calendarEvent, null) : (null, null, ManageResult.Fail(StaleError));
    }
}
