using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;

namespace Suanpan.Core.Calendar.Rules;

/// <summary>
/// 一个日程条目落到已有事件上时怎么处理（日历设计 §3.4）。排期只来自数据源、不可锁定（DEC-024）：
/// <list type="bullet">
/// <item>已公布、待复核、取消：已结束，不再处理</item>
/// <item>待公布、盯盘中：排期不同就改期（盯盘中的状态由盯盘服务处理）</item>
/// <item>延迟：官方改期后回到待公布（PRD §8.1.3）</item>
/// <item>待确认：重新出现在官方日程，回到待公布，排期不同时同时改期</item>
/// </list>
/// 状态被锁定时只改排期，不改状态（BR-062）。"排期相同"指 UTC 时间和时间精度都相同
/// </summary>
public static class ScheduleRule
{
    public static ScheduleDecision Decide(CalendarEvent current, DateTime? scheduledAt, TimePrecision precision)
    {
        if (!CalendarEventStatusRule.IsUnreleased(current.Status))
        {
            return ScheduleDecision.Ignore;
        }

        var reschedule = current.ScheduledAt != scheduledAt || current.TimePrecision != precision;
        CalendarEventStatus? newStatus = current.Status switch
        {
            CalendarEventStatus.Delayed when reschedule => CalendarEventStatus.Scheduled,
            CalendarEventStatus.Unconfirmed => CalendarEventStatus.Scheduled,
            _ => null,
        };
        if (EventLockRule.IsLocked(current.LockedFields, EventField.Status))
        {
            newStatus = null;
        }

        return new ScheduleDecision(false, reschedule, newStatus);
    }
}

/// <summary>排期比较的结果：Ignored 为事件已结束不处理；Reschedule 为要改期；NewStatus 为要改成的状态（不改时为空）</summary>
public sealed record ScheduleDecision(bool Ignored, bool Reschedule, CalendarEventStatus? NewStatus)
{
    public static readonly ScheduleDecision Ignore = new(true, false, null);

    /// <summary>有没有要改的：改期或改状态</summary>
    public bool HasChanges => Reschedule || NewStatus is not null;
}
