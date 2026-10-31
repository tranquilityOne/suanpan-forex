using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Rules;
using Suanpan.Core.Ops.Enums;
using Suanpan.Core.Reference.Enums;
using Suanpan.Data;
using Suanpan.Data.Interceptors;
using Suanpan.Services.Calendar.Events;
using Suanpan.Services.Common.Alerts;

namespace Suanpan.Services.Calendar.Watch;

public sealed class CalendarWatchService(
    SuanpanDbContext db,
    IAuditActorProvider auditActor,
    IAlertService alerts,
    TimeProvider timeProvider,
    ILogger<CalendarWatchService> logger) : ICalendarWatchService
{
    private const string StartReason = "进入盯盘窗口";
    private const string DelayReason = "排期后 10 分钟仍无值（ERR-EVT-001）";
    private const string RescheduledReason = "官方改期，退出盯盘";

    public async Task<IReadOnlyList<long>> StartAsync(IReadOnlyCollection<long> eventIds, long sourceId, long runId, CancellationToken ct = default)
    {
        var changed = await ChangeStatusAsync(eventIds, CalendarEventStatus.Watching, StartReason, sourceId, runId, ct);
        return changed.Select(e => e.Id).ToList();
    }

    public async Task<IReadOnlyList<long>> DelayAsync(IReadOnlyCollection<long> eventIds, long sourceId, long runId, CancellationToken ct = default)
    {
        // 只改盯盘中的：待复核 → 延迟是编辑驳回，不归盯盘管
        var changed = await ChangeStatusAsync(eventIds, CalendarEventStatus.Delayed, DelayReason, sourceId, runId, ct, onlyFrom: CalendarEventStatus.Watching);
        if (changed.Count == 0)
        {
            return [];
        }

        var codes = changed.Select(e => e.IndicatorCode).Distinct().ToList();
        var importance = await db.Indicators.AsNoTracking()
            .Where(i => codes.Contains(i.Code))
            .ToDictionaryAsync(i => i.Code, i => i.Importance, ct);
        foreach (var calendarEvent in changed)
        {
            var level = importance.GetValueOrDefault(calendarEvent.IndicatorCode) == Importance.High ? AlertLevel.Urgent : AlertLevel.Important;
            await RaiseAsync(level, calendarEvent.IndicatorCode,
                $"{calendarEvent.IndicatorCode} {calendarEvent.ReferencePeriod} 排期后 10 分钟仍未拿到公布值，已改为延迟（ERR-EVT-001），之后按配置的间隔补采", ct);
        }

        return changed.Select(e => e.Id).ToList();
    }

    public async Task<bool> ReturnToScheduledAsync(long eventId, long sourceId, long runId, CancellationToken ct = default)
        => (await ChangeStatusAsync([eventId], CalendarEventStatus.Scheduled, RescheduledReason, sourceId, runId, ct, onlyFrom: CalendarEventStatus.Watching))
            .Count > 0;

    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(IReadOnlyCollection<long> eventIds, CancellationToken ct = default)
        => await db.CalendarEvents.AsNoTracking().Where(e => eventIds.Contains(e.Id)).OrderBy(e => e.Id).ToListAsync(ct);

    /// <summary>
    /// 在行锁内改状态：按状态规则允许的才改（onlyFrom 不为空时还要求当前是这个状态），写状态历史、row_version 加一。
    /// 已经是目标状态的跳过
    /// </summary>
    private async Task<List<CalendarEvent>> ChangeStatusAsync(
        IReadOnlyCollection<long> eventIds, CalendarEventStatus status, string reason, long sourceId, long runId, CancellationToken ct,
        CalendarEventStatus? onlyFrom = null)
    {
        if (eventIds.Count == 0)
        {
            return [];
        }

        var ids = eventIds.Distinct().ToArray();
        var actor = new EventChangeActor(ChangeActorKind.Collector, auditActor.GetActor(), timeProvider.GetUtcNow().UtcDateTime, sourceId, runId);
        var changed = new List<CalendarEvent>();
        await db.InTransactionAsync(async () =>
        {
            // 行锁：与公布值入库（同样先锁事件行）、人工修改互斥
            var locked = await db.Database.SqlQuery<long>($"""
                SELECT id AS "Value" FROM cal_event WHERE id = ANY({ids}) ORDER BY id FOR UPDATE
                """).ToListAsync(ct);
            var events = await db.CalendarEvents.Where(e => locked.Contains(e.Id)).OrderBy(e => e.Id).ToListAsync(ct);
            foreach (var calendarEvent in events)
            {
                if (calendarEvent.Status == status || (onlyFrom is { } from && calendarEvent.Status != from)
                    || !CalendarEventStatusRule.CanTransition(calendarEvent.Status, status))
                {
                    continue;
                }

                db.CalendarEventChanges.Add(EventChanges.Create(calendarEvent.Id, EventChangeKind.Status,
                    EventChanges.Status(calendarEvent.Status), EventChanges.Status(status), actor, reason));
                calendarEvent.Status = status;
                calendarEvent.RowVersion++;
                changed.Add(calendarEvent);
            }

            await db.SaveChangesAsync(ct);
        }, ct);
        return changed;
    }

    /// <summary>告警是附带动作，失败只记日志（后端约定 §11）</summary>
    private async Task RaiseAsync(AlertLevel level, string subject, string message, CancellationToken ct)
    {
        try
        {
            await alerts.RaiseAsync(level, subject, message, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "告警发送失败：{Subject} {Message}", subject, message);
        }
    }
}
