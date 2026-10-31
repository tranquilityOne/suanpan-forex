using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;

namespace Suanpan.Core.Calendar.Rules;

/// <summary>
/// 编辑对日历事件的人工操作（日历设计 §3.7，BR-049 ~ BR-051）：哪些状态允许、操作后的状态。
/// 状态的变化同样要符合 <see cref="CalendarEventStatusRule"/>；人工操作不锁定状态（BR-061）
/// </summary>
public static class EventEditRule
{
    /// <summary>
    /// 录入或修改公布值后的状态：未公布的（待公布、盯盘中、延迟、待确认）和待复核的（改为人工值）改为"已公布"，
    /// 已公布的只改值仍为"已公布"；取消的不能改，返回 null
    /// </summary>
    public static CalendarEventStatus? ActualTarget(CalendarEventStatus status)
        => status == CalendarEventStatus.Cancelled ? null : CalendarEventStatus.Released;

    /// <summary>前值、前值修正：取消的事件不能改</summary>
    public static bool CanEditPrevious(CalendarEventStatus status) => status != CalendarEventStatus.Cancelled;

    /// <summary>待复核：确认采集值（采用新值）</summary>
    public static bool CanConfirm(CalendarEvent calendarEvent) => calendarEvent.Status == CalendarEventStatus.PendingReview;

    /// <summary>
    /// 待复核：驳回（转为"延迟"继续补采），只适用于从未公布过的事件，即公布值为空；
    /// 官方更正进入待复核的事件原公布值还在，只能采用新值或保留原值（BR-051）
    /// </summary>
    public static bool CanReject(CalendarEvent calendarEvent)
        => calendarEvent.Status == CalendarEventStatus.PendingReview && calendarEvent.Actual is null;

    /// <summary>
    /// 是否已经公布过：状态为已公布，或官方更正进入待复核（原公布值还在）。改了这类事件的数值要提示处理相关快讯（BR-052）
    /// </summary>
    public static bool WasPublished(CalendarEvent calendarEvent)
        => calendarEvent.Status == CalendarEventStatus.Released
           || (calendarEvent.Status == CalendarEventStatus.PendingReview && calendarEvent.Actual is not null);
}
