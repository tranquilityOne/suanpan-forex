namespace Suanpan.Core.Calendar.Enums;

/// <summary>日历事件变更历史的种类（cal_event_change，日历设计 §2.1）；改期历史即"改期"这一种（BR-039）</summary>
public enum EventChangeKind : short
{
    /// <summary>改期</summary>
    Rescheduled = 1,

    /// <summary>状态</summary>
    Status = 2,

    /// <summary>公布值</summary>
    Actual = 3,

    /// <summary>前值</summary>
    Previous = 4,

    /// <summary>前值修正</summary>
    PreviousRevised = 5,

    /// <summary>修正后数值</summary>
    RevisedValue = 6,

    /// <summary>预期值</summary>
    Forecast = 7,

    /// <summary>锁定</summary>
    Locked = 8,

    /// <summary>解锁</summary>
    Unlocked = 9,
}
