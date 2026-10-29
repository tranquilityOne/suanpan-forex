using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Rules;
using Suanpan.Data;
using Suanpan.Data.Interceptors;
using Suanpan.Services.Calendar.Events;
using Suanpan.Services.Calendar.Schedules.Models;
using Suanpan.Shared.Messages.Calendar;

namespace Suanpan.Services.Calendar.Schedules;

/// <summary>
/// 把一条排期写到日历事件上（日历设计 §4.3）。排期入库和未匹配条目的重新匹配共用；调用方负责事务，
/// 本类按"指标 + 参考期"行锁读取事件（§6.1），改了就加 row_version、写变更历史并保存：
/// <list type="bullet">
/// <item>没有事件：新建为"待公布"，前值取上一期已公布事件的公布值（BR-044）</item>
/// <item>事件的排期由别的数据源维护：不处理，同一事件只由一个数据源维护排期</item>
/// <item>事件没有维护排期的数据源（补建的、编辑"保留"过的）：由本数据源接管</item>
/// <item>按 ScheduleRule.Decide 改期、回到待公布（BR-039、PRD §8.1.3）</item>
/// </list>
/// 当地时刻不存在或重复时只标记、记 Warning 日志，不改状态（BR-037）
/// </summary>
public sealed class ScheduleApplier(
    SuanpanDbContext db,
    IAuditActorProvider auditActor,
    TimeProvider timeProvider,
    ILogger<ScheduleApplier> logger)
{
    public async Task<ScheduleApplyResult> ApplyAsync(ScheduleTarget target, CancellationToken ct)
    {
        var code = target.Indicator.Code;
        var period = target.ReferencePeriod;
        var lockedIds = await db.Database
            .SqlQuery<long>($"SELECT id AS \"Value\" FROM cal_event WHERE indicator_code = {code} AND reference_period = {period} FOR UPDATE")
            .ToListAsync(ct);
        if (lockedIds.Count == 0)
        {
            await CreateAsync(target, ct);
            return ScheduleApplyResult.Inserted;
        }

        var existing = await db.CalendarEvents.FirstAsync(e => e.Id == lockedIds[0], ct);
        if (existing.ScheduleSourceId is { } owner && owner != target.SourceId)
        {
            logger.LogInformation("事件 {IndicatorCode} {ReferencePeriod} 的排期由数据源 {Owner} 维护，数据源 {SourceId} 的条目不处理",
                code, period, owner, target.SourceId);
            return ScheduleApplyResult.Skipped;
        }

        var decision = ScheduleRule.Decide(existing, target.Schedule.Utc, target.Precision);
        var takeOver = existing.ScheduleSourceId is null
            || existing.ScheduleReleaseKey != target.ReleaseKey
            || existing.ReleaseBatchId != target.ReleaseBatchId;
        if (decision.Ignored || (!decision.HasChanges && !takeOver))
        {
            return ScheduleApplyResult.Skipped;
        }

        var actor = Actor(target);
        existing.ScheduleSourceId = target.SourceId;
        existing.ScheduleReleaseKey = target.ReleaseKey;
        existing.ReleaseBatchId = target.ReleaseBatchId;

        EventRescheduledEvent? rescheduled = null;
        if (decision.Reschedule)
        {
            db.CalendarEventChanges.Add(EventChanges.Create(existing.Id, EventChangeKind.Rescheduled,
                EventChanges.Schedule(existing.ScheduledAt, existing.ScheduledLocal, existing.TimePrecision),
                EventChanges.Schedule(target.Schedule.Utc, target.Schedule.Local, target.Precision),
                actor, "官方改期"));
            rescheduled = new EventRescheduledEvent(existing.Id, code, period, existing.ScheduledAt, target.Schedule.Utc, target.Precision);
            SetSchedule(existing, target);
        }

        if (decision.NewStatus is { } status)
        {
            var reason = existing.Status == CalendarEventStatus.Delayed ? "官方改期" : "重新出现在官方日程";
            db.CalendarEventChanges.Add(EventChanges.Create(existing.Id, EventChangeKind.Status,
                EventChanges.Status(existing.Status), EventChanges.Status(status), actor, reason));
            existing.Status = status;
        }

        existing.RowVersion++;
        await db.SaveChangesAsync(ct);
        return new ScheduleApplyResult(ScheduleApplyOutcome.Updated, rescheduled);
    }

    private async Task CreateAsync(ScheduleTarget target, CancellationToken ct)
    {
        var created = new CalendarEvent
        {
            IndicatorCode = target.Indicator.Code,
            ReferencePeriod = target.ReferencePeriod,
            Status = CalendarEventStatus.Scheduled,
            Origin = EventOrigin.Collected,
            ReleaseBatchId = target.ReleaseBatchId,
            ScheduleSourceId = target.SourceId,
            ScheduleReleaseKey = target.ReleaseKey,
        };
        SetSchedule(created, target);

        var previous = await PreviousReleaseAsync(target, ct);
        created.Previous = previous?.Actual;
        created.PreviousUpper = previous?.ActualUpper;

        db.CalendarEvents.Add(created);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 上一期已公布事件的公布值（BR-044）：月、季、周按 ReferencePeriodRule.Previous 找上一期；
    /// 不定期（如议息会议）取更早的最近一期。上一期还没有公布值时为空，公布时再填（日历设计 §5.3）
    /// </summary>
    private async Task<PreviousRelease?> PreviousReleaseAsync(ScheduleTarget target, CancellationToken ct)
    {
        var code = target.Indicator.Code;
        var period = target.ReferencePeriod;
        var earlier = db.CalendarEvents.AsNoTracking().Where(e => e.IndicatorCode == code);
        earlier = ReferencePeriodRule.Previous(period, target.Indicator.Frequency) is { } previousPeriod
            ? earlier.Where(e => e.ReferencePeriod == previousPeriod)
            : earlier.Where(e => string.Compare(e.ReferencePeriod, period) < 0);
        return await earlier
            .Where(e => e.Actual != null)
            .OrderByDescending(e => e.ReferencePeriod)
            .Select(e => new PreviousRelease(e.Actual, e.ActualUpper))
            .FirstOrDefaultAsync(ct);
    }

    private void SetSchedule(CalendarEvent calendarEvent, ScheduleTarget target)
    {
        calendarEvent.ScheduledLocal = target.Schedule.Local;
        calendarEvent.TimeZone = target.Indicator.TimeZone;
        calendarEvent.ScheduledAt = target.Schedule.Utc;
        calendarEvent.TimePrecision = target.Precision;
        calendarEvent.LocalTimeResolution = target.Schedule.Resolution;
        if (target.Schedule.Resolution != LocalTimeResolution.Exact)
        {
            logger.LogWarning("事件 {IndicatorCode} {ReferencePeriod} 的当地时间 {Local}（{TimeZone}）落在夏令时切换时段：{Resolution}",
                target.Indicator.Code, target.ReferencePeriod, target.Schedule.Local, target.Indicator.TimeZone,
                CalendarDisplayNames.Get(target.Schedule.Resolution));
        }
    }

    private EventChangeActor Actor(ScheduleTarget target)
        => new(ChangeActorKind.Collector, auditActor.GetActor(), timeProvider.GetUtcNow().UtcDateTime, target.SourceId, target.RunId);

    private sealed record PreviousRelease(decimal? Actual, decimal? ActualUpper);
}
