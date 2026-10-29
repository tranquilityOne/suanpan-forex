using Suanpan.Core.Calendar.Enums;

namespace Suanpan.Core.Calendar.Rules;

/// <summary>
/// 日历事件的状态流转（PRD §8.1.3，日历设计 §3.2）：状态只能按下表流转，服务和入库在改状态前检查；
/// 人工操作不符合时提示当前状态允许的操作（ERR-EVT-010）
/// </summary>
public static class CalendarEventStatusRule
{
    private static readonly Dictionary<CalendarEventStatus, CalendarEventStatus[]> Transitions = new()
    {
        // 进入盯盘窗口；补采或人工录入；补采值校验未通过；从官方日程中消失；编辑取消
        [CalendarEventStatus.Scheduled] =
        [
            CalendarEventStatus.Watching, CalendarEventStatus.Released, CalendarEventStatus.PendingReview,
            CalendarEventStatus.Unconfirmed, CalendarEventStatus.Cancelled,
        ],

        // 编辑保留或重新出现在官方日程；采到值（校验通过 / 未通过）；编辑确认取消
        [CalendarEventStatus.Unconfirmed] =
        [
            CalendarEventStatus.Scheduled, CalendarEventStatus.Released, CalendarEventStatus.PendingReview, CalendarEventStatus.Cancelled,
        ],

        // 拿到值（校验通过 / 未通过）；T+10 分钟无值
        [CalendarEventStatus.Watching] = [CalendarEventStatus.Released, CalendarEventStatus.PendingReview, CalendarEventStatus.Delayed],

        // 官方改期；补采到值（校验通过 / 未通过）
        [CalendarEventStatus.Delayed] = [CalendarEventStatus.Scheduled, CalendarEventStatus.Released, CalendarEventStatus.PendingReview],

        // 编辑确认或改为人工值；编辑驳回，继续补采
        [CalendarEventStatus.PendingReview] = [CalendarEventStatus.Released, CalendarEventStatus.Delayed],

        // 再次采集到不同的公布值（BR-046）
        [CalendarEventStatus.Released] = [CalendarEventStatus.PendingReview],

        [CalendarEventStatus.Cancelled] = [],
    };

    public static bool CanTransition(CalendarEventStatus from, CalendarEventStatus to)
        => Transitions.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>从某个状态出发允许的下一个状态，提示 ERR-EVT-010 时使用</summary>
    public static IReadOnlyList<CalendarEventStatus> NextStatuses(CalendarEventStatus from)
        => Transitions.TryGetValue(from, out var targets) ? targets : [];

    /// <summary>未公布：待公布、盯盘中、延迟、待确认。排期入库只处理未公布的事件（日历设计 §3.4）</summary>
    public static bool IsUnreleased(CalendarEventStatus status)
        => status is CalendarEventStatus.Scheduled or CalendarEventStatus.Watching or CalendarEventStatus.Delayed or CalendarEventStatus.Unconfirmed;
}
