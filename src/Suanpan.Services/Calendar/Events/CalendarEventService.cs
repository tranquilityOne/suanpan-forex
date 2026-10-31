using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Rules;
using Suanpan.Core.Reference.Rules;
using Suanpan.Data;
using Suanpan.Data.Interceptors;
using Suanpan.Services.Calendar.Events.Models;
using Suanpan.Services.Common.Querying;
using Suanpan.Shared.Mvc;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Calendar.Events;

public sealed class CalendarEventService(SuanpanDbContext db, IAuditActorProvider auditActor, TimeProvider timeProvider) : ICalendarEventService
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
        if (string.IsNullOrEmpty(reason))
        {
            return ManageResult.Fail("请填写取消原因", "Reason");
        }

        if (reason.Length > ICalendarEventService.MaxReasonLength)
        {
            return ManageResult.Fail($"取消原因不能超过 {ICalendarEventService.MaxReasonLength} 个字", "Reason");
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

        return await ChangeStatusAsync(calendarEvent, CalendarEventStatus.Cancelled, reason, ct);
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
