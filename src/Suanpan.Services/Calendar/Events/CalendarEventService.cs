using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Rules;
using Suanpan.Core.Reference.Enums;
using Suanpan.Core.Reference.Rules;
using Suanpan.Data;
using Suanpan.Data.Interceptors;
using Suanpan.Infrastructure.Messaging;
using Suanpan.Services.Calendar.Events.Models;
using Suanpan.Services.Common.Querying;
using Suanpan.Shared.Messages;
using Suanpan.Shared.Messages.Calendar;
using Suanpan.Shared.Mvc;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Calendar.Events;

public sealed class CalendarEventService(
    SuanpanDbContext db,
    IAuditActorProvider auditActor,
    IEventBus eventBus,
    TimeProvider timeProvider,
    ILogger<CalendarEventService> logger) : ICalendarEventService
{
    private const string StaleError = "事件已被修改，请刷新页面后重试";

    public Task<PagedResult<EventRow>> QueryAsync(EventQuery query, CancellationToken ct = default)
    {
        var events = db.CalendarEvents.AsNoTracking();
        if (query.Begin is { } begin)
        {
            events = events.Where(e => e.ScheduledAt >= begin);
        }

        if (query.End is { } end)
        {
            events = events.Where(e => e.ScheduledAt < end);
        }

        if (query.Status is { } status)
        {
            events = events.Where(e => e.Status == status);
        }

        if (query.MissingForecast)
        {
            events = events.Where(e => e.Forecast == null);
        }

        var indicators = db.Indicators.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.EconomyCode))
        {
            var economyCode = ReferenceCodeRule.Normalize(query.EconomyCode);
            indicators = indicators.Where(i => i.EconomyCode == economyCode);
        }

        if (query.Importance is { } importance)
        {
            indicators = indicators.Where(i => i.Importance == importance);
        }

        return (from e in events
                join i in indicators on e.IndicatorCode equals i.Code
                orderby e.ScheduledAt, e.Id
                select new EventRow(e, i.ShortName, i.EconomyCode, i.Importance, i.ValueType, i.Unit, i.Decimals))
            .ToPagedResultAsync(query.Page, query.PageSize, ct);
    }

    public async Task<EventDetail?> GetDetailAsync(long id, CancellationToken ct = default)
    {
        if (await db.CalendarEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct) is not { } calendarEvent
            || await db.Indicators.AsNoTracking().FirstOrDefaultAsync(i => i.Code == calendarEvent.IndicatorCode, ct) is not { } indicator)
        {
            return null;
        }

        var values = await db.CalendarEventValues.AsNoTracking().Where(v => v.EventId == id).OrderBy(v => v.SourceId).ToListAsync(ct);
        var conflicts = await db.LockConflicts.AsNoTracking()
            .Where(c => c.EventId == id)
            .OrderBy(c => c.Status).ThenByDescending(c => c.LastDetectedAt)
            .ToListAsync(ct);
        var changes = await db.CalendarEventChanges.AsNoTracking()
            .Where(c => c.EventId == id)
            .OrderByDescending(c => c.ChangedAt).ThenByDescending(c => c.Id)
            .ToListAsync(ct);

        long?[] referenced = [calendarEvent.ScheduleSourceId, calendarEvent.ActualSourceId];
        var sourceIds = referenced.OfType<long>()
            .Concat(values.Select(v => v.SourceId))
            .Concat(conflicts.Select(c => c.SourceId))
            .Concat(changes.Select(c => c.SourceId).OfType<long>())
            .Distinct()
            .ToList();
        var sourceNames = await db.DataSources.AsNoTracking()
            .Where(s => sourceIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        return new EventDetail(calendarEvent, indicator, values, conflicts, changes, sourceNames);
    }

    public Task<PagedResult<UnconfirmedEventRow>> QueryUnconfirmedAsync(UnconfirmedEventQuery query, CancellationToken ct = default)
    {
        var events = db.CalendarEvents.AsNoTracking().Where(e => e.Status == CalendarEventStatus.Unconfirmed);
        if (!string.IsNullOrWhiteSpace(query.IndicatorCode))
        {
            var code = ReferenceCodeRule.Normalize(query.IndicatorCode);
            events = events.Where(e => e.IndicatorCode == code);
        }

        return Rows(events).ToPagedResultAsync(query.Page, query.PageSize, ct);
    }

    public Task<UnconfirmedEventRow?> GetUnconfirmedAsync(long id, CancellationToken ct = default)
        => Rows(db.CalendarEvents.AsNoTracking().Where(e => e.Id == id && e.Status == CalendarEventStatus.Unconfirmed)).FirstOrDefaultAsync(ct);

    public async Task<ManageResult> KeepAsync(long id, int rowVersion, CancellationToken ct = default)
    {
        var (calendarEvent, error) = await FindAsync(id, rowVersion, ct);
        if (calendarEvent is null)
        {
            return error!;
        }

        if (calendarEvent.Status != CalendarEventStatus.Unconfirmed)
        {
            return StatusError(calendarEvent, "保留");
        }

        calendarEvent.ScheduleSourceId = null;
        calendarEvent.ScheduleReleaseKey = null;
        return await ChangeStatusAsync(calendarEvent, CalendarEventStatus.Scheduled, "编辑保留，与数据源脱钩", ct);
    }

    public async Task<ManageResult> CancelAsync(long id, string? reason, int rowVersion, CancellationToken ct = default)
    {
        reason = reason?.Trim();
        if (CheckReason(reason, "取消原因") is { } reasonError)
        {
            return reasonError;
        }

        var (calendarEvent, error) = await FindAsync(id, rowVersion, ct);
        if (calendarEvent is null)
        {
            return error!;
        }

        if (!CalendarEventStatusRule.CanTransition(calendarEvent.Status, CalendarEventStatus.Cancelled))
        {
            return StatusError(calendarEvent, "取消");
        }

        return await ChangeStatusAsync(calendarEvent, CalendarEventStatus.Cancelled, reason!, ct);
    }

    public async Task<ManageResult> EditValuesAsync(long id, EventValuesInput input, string? reason, int rowVersion, CancellationToken ct = default)
    {
        reason = reason?.Trim();
        if (CheckReason(reason, "修改原因") is { } reasonError)
        {
            return reasonError;
        }

        var (calendarEvent, error) = await FindAsync(id, rowVersion, ct);
        if (calendarEvent is null)
        {
            return error!;
        }

        var indicator = await db.Indicators.AsNoTracking().FirstAsync(i => i.Code == calendarEvent.IndicatorCode, ct);

        // 待复核的事件填了公布值即为"改为人工值"，与原值相同也写入（保留原值，BR-051）
        var actualChanged = input.Actual is { } actual
            && (calendarEvent.Status == CalendarEventStatus.PendingReview || actual != calendarEvent.Actual || input.ActualUpper != calendarEvent.ActualUpper);
        var previousChanged = input.Previous is { } previous && (previous != calendarEvent.Previous || input.PreviousUpper != calendarEvent.PreviousUpper);
        var revisedChanged = input.PreviousRevised is { } revised && revised != calendarEvent.PreviousRevised;
        if (!actualChanged && !previousChanged && !revisedChanged)
        {
            return ManageResult.Fail("没有修改任何数值");
        }

        if (actualChanged && EventEditRule.ActualTarget(calendarEvent.Status) is null
            || (previousChanged || revisedChanged) && !EventEditRule.CanEditPrevious(calendarEvent.Status))
        {
            return StatusError(calendarEvent, "修改数值");
        }

        var formatError = (actualChanged ? Check(nameof(input.Actual), input.Actual!.Value, input.ActualUpper, indicator.ValueType, indicator.Decimals) : null)
            ?? (previousChanged ? Check(nameof(input.Previous), input.Previous!.Value, input.PreviousUpper, indicator.ValueType, indicator.Decimals) : null)
            ?? (revisedChanged && indicator.ValueType == IndicatorValueType.Range ? ManageResult.Fail("区间型指标没有前值修正", nameof(input.PreviousRevised)) : null)
            ?? (revisedChanged ? Check(nameof(input.PreviousRevised), input.PreviousRevised!.Value, null, IndicatorValueType.Value, indicator.Decimals) : null);
        if (formatError is not null)
        {
            return formatError;
        }

        var actor = UserActor();
        var wasPublished = EventEditRule.WasPublished(calendarEvent);
        var changed = new List<EventField>();
        var messages = new List<IntegrationMessage>();
        var locks = calendarEvent.LockedFields.ToList();
        if (actualChanged)
        {
            var (value, upper) = (input.Actual!.Value, input.ActualUpper);
            db.CalendarEventChanges.Add(EventChanges.Create(id, EventChangeKind.Actual,
                EventChanges.Number(calendarEvent.Actual), EventChanges.Number(value), actor, reason));
            calendarEvent.Actual = value;
            calendarEvent.ActualUpper = upper;
            calendarEvent.ActualRaw = null;
            calendarEvent.ActualSourceId = null;
            calendarEvent.SourceSnapshotId = null;
            calendarEvent.DetectedAt = actor.ChangedAt;
            EventLockRule.Lock(locks, EventField.Actual, actor.ChangedBy, actor.ChangedAt, reason!);
            if (calendarEvent.Status != CalendarEventStatus.Released)
            {
                ReleaseManually(calendarEvent, actor, reason!, actor.ChangedAt, messages);
            }

            await NextEventPrevious.FillAsync(db, calendarEvent, value, upper, actor, ct);
            changed.Add(EventField.Actual);
        }

        if (previousChanged)
        {
            db.CalendarEventChanges.Add(EventChanges.Create(id, EventChangeKind.Previous,
                EventChanges.Number(calendarEvent.Previous), EventChanges.Number(input.Previous), actor, reason));
            calendarEvent.Previous = input.Previous;
            calendarEvent.PreviousUpper = input.PreviousUpper;
            EventLockRule.Lock(locks, EventField.Previous, actor.ChangedBy, actor.ChangedAt, reason!);
            changed.Add(EventField.Previous);
        }

        if (revisedChanged)
        {
            db.CalendarEventChanges.Add(EventChanges.Create(id, EventChangeKind.PreviousRevised,
                EventChanges.Number(calendarEvent.PreviousRevised), EventChanges.Number(input.PreviousRevised), actor, reason));
            calendarEvent.PreviousRevised = input.PreviousRevised;
            EventLockRule.Lock(locks, EventField.PreviousRevised, actor.ChangedBy, actor.ChangedAt, reason!);
            changed.Add(EventField.PreviousRevised);
        }

        // 赋一个新的列表，jsonb 列的修改才会被检测到
        calendarEvent.LockedFields = locks;
        if (wasPublished)
        {
            messages.Add(new EventValueEditedEvent(id, calendarEvent.IndicatorCode, calendarEvent.ReferencePeriod, changed, actor.ChangedBy));
        }

        return await SaveAsync(calendarEvent, messages, ct);
    }

    public async Task<ManageResult> UnlockAsync(long id, EventField field, int rowVersion, CancellationToken ct = default)
    {
        var (calendarEvent, error) = await FindAsync(id, rowVersion, ct);
        if (calendarEvent is null)
        {
            return error!;
        }

        var locks = calendarEvent.LockedFields.ToList();
        if (!EventLockRule.Unlock(locks, field))
        {
            return ManageResult.Fail($"{CalendarDisplayNames.Get(field)}没有锁定");
        }

        var actor = UserActor();
        calendarEvent.LockedFields = locks;
        db.CalendarEventChanges.Add(EventChanges.Create(id, EventChangeKind.Unlocked, EventChanges.Field(field), null, actor));

        // 这个字段待处理的冲突一并关闭：解锁后下一次采集照常更新，冲突不用再处理（日历设计 §3.9）
        var conflicts = await db.LockConflicts.Where(c => c.EventId == id && c.Field == field && c.Status == LockConflictStatus.Pending).ToListAsync(ct);
        foreach (var conflict in conflicts)
        {
            conflict.Status = LockConflictStatus.Resolved;
            conflict.Resolution = LockConflictResolution.Unlocked;
            conflict.ResolvedBy = actor.ChangedBy;
            conflict.ResolvedAt = actor.ChangedAt;
        }

        return await SaveAsync(calendarEvent, [], ct);
    }

    public async Task<PagedResult<ReviewRow>> QueryReviewAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await (
                from e in db.CalendarEvents.AsNoTracking()
                where e.Status == CalendarEventStatus.PendingReview
                join i in db.Indicators on e.IndicatorCode equals i.Code
                orderby e.ScheduledAt, e.Id
                select new EventRow(e, i.ShortName, i.EconomyCode, i.Importance, i.ValueType, i.Unit, i.Decimals))
            .ToPagedResultAsync(page, pageSize, ct);
        var ids = result.Items.Select(r => r.Event.Id).ToList();
        var statusChanges = await db.CalendarEventChanges.AsNoTracking()
            .Where(c => ids.Contains(c.EventId) && c.Kind == EventChangeKind.Status)
            .OrderByDescending(c => c.ChangedAt).ThenByDescending(c => c.Id)
            .ToListAsync(ct);
        var candidates = await db.CalendarEventValues.AsNoTracking().Where(v => ids.Contains(v.EventId)).OrderBy(v => v.SourceId).ToListAsync(ct);
        var sourceIds = candidates.Select(v => v.SourceId).Distinct().ToList();
        var sourceNames = await db.DataSources.AsNoTracking().Where(s => sourceIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var rows = result.Items
            .Select(r => new ReviewRow(
                r,
                statusChanges.FirstOrDefault(c => c.EventId == r.Event.Id)?.Reason,
                candidates.Where(v => v.EventId == r.Event.Id).ToList(),
                sourceNames))
            .ToList();
        return new PagedResult<ReviewRow>(rows, result.Total, result.Page, result.PageSize);
    }

    public async Task<ManageResult> ConfirmReviewAsync(long id, long sourceId, int rowVersion, CancellationToken ct = default)
    {
        var (calendarEvent, error) = await FindAsync(id, rowVersion, ct);
        if (calendarEvent is null)
        {
            return error!;
        }

        if (!EventEditRule.CanConfirm(calendarEvent))
        {
            return StatusError(calendarEvent, "确认采集值");
        }

        if (await db.CalendarEventValues.AsNoTracking().FirstOrDefaultAsync(v => v.EventId == id && v.SourceId == sourceId, ct) is not { } candidate)
        {
            return ManageResult.Fail("这个数据源没有候选值");
        }

        const string reason = "编辑确认采集值";
        var actor = UserActor();
        var wasPublished = EventEditRule.WasPublished(calendarEvent);
        var valueChanged = calendarEvent.Actual != candidate.Value || calendarEvent.ActualUpper != candidate.ValueUpper;
        var messages = new List<IntegrationMessage>();
        if (valueChanged)
        {
            db.CalendarEventChanges.Add(EventChanges.Create(id, EventChangeKind.Actual,
                EventChanges.Number(calendarEvent.Actual), EventChanges.Number(candidate.Value), actor, reason));
        }

        calendarEvent.Actual = candidate.Value;
        calendarEvent.ActualUpper = candidate.ValueUpper;
        calendarEvent.ActualRaw = candidate.Raw;
        calendarEvent.ActualSourceId = candidate.SourceId;
        calendarEvent.SourceSnapshotId = candidate.SnapshotId;
        calendarEvent.DetectedAt = candidate.DetectedAt;
        ReleaseManually(calendarEvent, actor, reason, candidate.DetectedAt, messages);
        await NextEventPrevious.FillAsync(db, calendarEvent, candidate.Value, candidate.ValueUpper, actor, ct);
        if (wasPublished && valueChanged)
        {
            messages.Add(new EventValueEditedEvent(id, calendarEvent.IndicatorCode, calendarEvent.ReferencePeriod, [EventField.Actual], actor.ChangedBy));
        }

        return await SaveAsync(calendarEvent, messages, ct);
    }

    public async Task<ManageResult> RejectReviewAsync(long id, string? reason, int rowVersion, CancellationToken ct = default)
    {
        reason = reason?.Trim();
        if (CheckReason(reason, "驳回原因") is { } reasonError)
        {
            return reasonError;
        }

        var (calendarEvent, error) = await FindAsync(id, rowVersion, ct);
        if (calendarEvent is null)
        {
            return error!;
        }

        if (!EventEditRule.CanReject(calendarEvent))
        {
            return calendarEvent.Status == CalendarEventStatus.PendingReview
                ? ManageResult.Fail("事件已经公布过，不能驳回：请选择采用新值，或改为人工值保留原值")
                : StatusError(calendarEvent, "驳回");
        }

        return await ChangeStatusAsync(calendarEvent, CalendarEventStatus.Delayed, reason!, ct);
    }

    /// <summary>原因必填、不超过 500 字（ERR-EVT-008）；reason 已去掉首尾空白</summary>
    private static ManageResult? CheckReason(string? reason, string label)
        => string.IsNullOrEmpty(reason) ? ManageResult.Fail($"请填写{label}", "Reason")
            : reason.Length > ICalendarEventService.MaxReasonLength ? ManageResult.Fail($"{label}不能超过 {ICalendarEventService.MaxReasonLength} 个字", "Reason")
            : null;

    /// <summary>数值格式（BR-058）：不对时 Field 为对应的字段名</summary>
    private static ManageResult? Check(string field, decimal value, decimal? upper, IndicatorValueType valueType, short decimals)
        => EventValueRule.Validate(value, upper, valueType, decimals) is { } message ? ManageResult.Fail(message, field) : null;

    private EventChangeActor UserActor() => new(ChangeActorKind.User, auditActor.GetActor(), timeProvider.GetUtcNow().UtcDateTime);

    /// <summary>人工录入或确认后改为"已公布"，写状态变更历史，提交后发出 EventReleasedEvent（EV-004）</summary>
    private void ReleaseManually(CalendarEvent calendarEvent, EventChangeActor actor, string reason, DateTime detectedAt, List<IntegrationMessage> messages)
    {
        db.CalendarEventChanges.Add(EventChanges.Create(calendarEvent.Id, EventChangeKind.Status,
            EventChanges.Status(calendarEvent.Status), EventChanges.Status(CalendarEventStatus.Released), actor, reason));
        calendarEvent.Status = CalendarEventStatus.Released;
        messages.Add(new EventReleasedEvent(
            calendarEvent.Id, calendarEvent.IndicatorCode, calendarEvent.ReferencePeriod, calendarEvent.ReleaseBatchId, detectedAt));
    }

    /// <summary>保存人工修改：row_version 加一，并发修改时按 ERR-EVT-009 拒绝；提交后发消息，发布失败只记日志</summary>
    private async Task<ManageResult> SaveAsync(CalendarEvent calendarEvent, List<IntegrationMessage> messages, CancellationToken ct)
    {
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

        foreach (var message in messages)
        {
            try
            {
                await eventBus.PublishAsync(message, ct: ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "消息 {MessageName} 发布失败", message.MessageName);
            }
        }

        return ManageResult.Ok;
    }

    /// <summary>读出要改的事件；不存在，或 row_version 与读取时不同（被他人或采集改过，ERR-EVT-009）时返回错误</summary>
    private async Task<(CalendarEvent? Event, ManageResult? Error)> FindAsync(long id, int rowVersion, CancellationToken ct)
    {
        var calendarEvent = await db.CalendarEvents.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (calendarEvent is null)
        {
            return (null, ManageResult.Fail("事件不存在"));
        }

        return calendarEvent.RowVersion == rowVersion ? (calendarEvent, null) : (null, ManageResult.Fail(StaleError));
    }

    /// <summary>改状态并写人工的变更历史；保存时 row_version 已被并发修改的，按 ERR-EVT-009 拒绝</summary>
    private async Task<ManageResult> ChangeStatusAsync(CalendarEvent calendarEvent, CalendarEventStatus status, string reason, CancellationToken ct)
    {
        var actor = new EventChangeActor(ChangeActorKind.User, auditActor.GetActor(), timeProvider.GetUtcNow().UtcDateTime);
        db.CalendarEventChanges.Add(EventChanges.Create(calendarEvent.Id, EventChangeKind.Status,
            EventChanges.Status(calendarEvent.Status), EventChanges.Status(status), actor, reason));
        calendarEvent.Status = status;
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

        return ManageResult.Ok;
    }

    /// <summary>状态不允许这个操作（ERR-EVT-010）</summary>
    private static ManageResult StatusError(CalendarEvent calendarEvent, string operation)
        => ManageResult.Fail($"事件当前为“{CalendarDisplayNames.Get(calendarEvent.Status)}”，不能{operation}");

    /// <summary>
    /// 按排期时间排序（没有排期时间的排在最后，PostgreSQL 升序默认如此）。
    /// 变为待确认的时间：当前状态是待确认，最近一次状态变更就是变为待确认的那一次
    /// </summary>
    private IQueryable<UnconfirmedEventRow> Rows(IQueryable<CalendarEvent> events)
        => from e in events
           join i in db.Indicators on e.IndicatorCode equals i.Code
           join s in db.DataSources on e.ScheduleSourceId equals (long?)s.Id into sources
           from s in sources.DefaultIfEmpty()
           orderby e.ScheduledAt, e.Id
           select new UnconfirmedEventRow(
               e.Id, e.IndicatorCode, i.ShortName, e.ReferencePeriod, e.ScheduledAt, e.ScheduledLocal, e.TimeZone, e.TimePrecision,
               e.ScheduleSourceId, s == null ? null : s.Name,
               db.CalendarEventChanges.Where(c => c.EventId == e.Id && c.Kind == EventChangeKind.Status).Max(c => (DateTime?)c.ChangedAt),
               e.RowVersion);
}
