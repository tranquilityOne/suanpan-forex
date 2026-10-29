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
    /// <summary>日程条目的日期时间是否齐全：精确到分钟要有日期和时间，仅日期要有日期，待定可以都没有。齐全时返回 null</summary>
    public static string? ValidateEntry(DateOnly? localDate, TimeOnly? localTime, TimePrecision precision) => precision switch
    {
        TimePrecision.Minute when localDate is null || localTime is null => "精确到分钟的排期缺少日期或时间",
        TimePrecision.DateOnly when localDate is null => "仅日期的排期缺少日期",
        _ => null,
    };

    /// <summary>
    /// 把机构当地的日期时间换算成排期（日历设计 §4.3 第 1 步，BR-037）：精确到分钟的按 LocalTimeRule 换算；
    /// 仅日期、待定的取当地日期 00:00（只用于排序）；待定且没有日期时都为空。调用前先用 <see cref="ValidateEntry"/> 校验
    /// </summary>
    public static ResolvedSchedule Resolve(DateOnly? localDate, TimeOnly? localTime, TimePrecision precision, string ianaTimeZone)
    {
        if (localDate is not { } date)
        {
            return new ResolvedSchedule(null, null, LocalTimeResolution.Exact);
        }

        var time = precision == TimePrecision.Minute ? localTime!.Value : TimeOnly.MinValue;
        var conversion = LocalTimeRule.ToUtc(date, time, ianaTimeZone);
        return new ResolvedSchedule(date.ToDateTime(time, DateTimeKind.Unspecified), conversion.Utc, conversion.Resolution);
    }

    /// <summary>
    /// 只处理未来的条目（日历设计 §4.2）：精确到分钟的按 UTC 时间判断；仅日期、待定的按机构当地的日期判断，当天算未来；
    /// 待定且没有日期的算未来
    /// </summary>
    public static bool IsFuture(ResolvedSchedule schedule, TimePrecision precision, string ianaTimeZone, DateTime nowUtc)
    {
        if (schedule.Local is not { } local)
        {
            return true;
        }

        return precision == TimePrecision.Minute
            ? schedule.Utc > nowUtc
            : DateOnly.FromDateTime(local) >= LocalTimeRule.LocalDate(nowUtc, ianaTimeZone);
    }

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

/// <summary>
/// 换算后的排期：Local 为机构当地时间（Kind 为 Unspecified，仅日期时为 00:00），Utc 为 UTC 时间，
/// Resolution 为当地时刻属于哪种情况（BR-037）；待定且没有日期时 Local、Utc 都为空
/// </summary>
public sealed record ResolvedSchedule(DateTime? Local, DateTime? Utc, LocalTimeResolution Resolution);

/// <summary>排期比较的结果：Ignored 为事件已结束不处理；Reschedule 为要改期；NewStatus 为要改成的状态（不改时为空）</summary>
public sealed record ScheduleDecision(bool Ignored, bool Reschedule, CalendarEventStatus? NewStatus)
{
    public static readonly ScheduleDecision Ignore = new(true, false, null);

    /// <summary>有没有要改的：改期或改状态</summary>
    public bool HasChanges => Reschedule || NewStatus is not null;
}
