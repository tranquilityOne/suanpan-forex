namespace Suanpan.Core.Calendar.Enums;

/// <summary>日历事件的状态（BR-049，PRD §8.1.3，日历设计 §3.2）；流转规则见 CalendarEventStatusRule</summary>
public enum CalendarEventStatus : short
{
    /// <summary>待公布</summary>
    Scheduled = 1,

    /// <summary>盯盘中（FR-003）</summary>
    Watching = 2,

    /// <summary>已公布</summary>
    Released = 3,

    /// <summary>待复核：校验未通过或官方更正（BR-046、FR-026）</summary>
    PendingReview = 4,

    /// <summary>延迟：排期后 10 分钟仍无值（BR-017）</summary>
    Delayed = 5,

    /// <summary>待确认：从官方日程中消失（BR-040）</summary>
    Unconfirmed = 6,

    /// <summary>取消</summary>
    Cancelled = 7,
}
