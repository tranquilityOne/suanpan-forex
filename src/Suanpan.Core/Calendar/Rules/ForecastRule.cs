using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;

namespace Suanpan.Core.Calendar.Rules;

/// <summary>
/// 预期值（日历设计 §3.8，FR-010，BR-053 ~ BR-059）：公布前由编辑录入，公布后只有管理员能更正（BR-057）；
/// 待录入清单列出未来 7 天的高重要性事件（BR-055），公布前 24 小时仍未录入的提醒（BR-056）
/// </summary>
public static class ForecastRule
{
    /// <summary>待录入清单的范围：从现在起的天数（BR-055）</summary>
    public const int PendingDays = 7;

    /// <summary>来源说明的长度上限（BR-054）</summary>
    public const int MaxNoteLength = 100;

    /// <summary>公布前多久仍未录入就提醒（BR-056）</summary>
    public static readonly TimeSpan ReminderWindow = TimeSpan.FromHours(24);

    /// <summary>"公布后"（BR-057，§12 第 18 项）：状态为已公布、待复核，或公布值不为空</summary>
    public static bool IsReleased(CalendarEvent calendarEvent)
        => calendarEvent.Status is CalendarEventStatus.Released or CalendarEventStatus.PendingReview || calendarEvent.Actual is not null;

    /// <summary>公布前录入、修改（管理员、编辑）：未公布，且不是取消</summary>
    public static bool CanEnter(CalendarEvent calendarEvent)
        => calendarEvent.Status != CalendarEventStatus.Cancelled && !IsReleased(calendarEvent);

    /// <summary>管理员更正：取消的事件不能</summary>
    public static bool CanCorrect(CalendarEvent calendarEvent) => calendarEvent.Status != CalendarEventStatus.Cancelled;
}
