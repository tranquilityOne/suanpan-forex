using Suanpan.Core.Calendar.Enums;

namespace Suanpan.Core.Calendar.Rules;

/// <summary>
/// 财经日历模块枚举的中文显示名（日历设计 §3.6），只在后台使用；前台的中英文文字由对外视图和前台自己处理。
/// 枚举增加取值时在这里登记，每个值都要有对应项（测试会检查）。
/// </summary>
public static class CalendarDisplayNames
{
    private static readonly Dictionary<Enum, string> Map = new()
    {
        [CalendarEventStatus.Scheduled] = "待公布",
        [CalendarEventStatus.Watching] = "盯盘中",
        [CalendarEventStatus.Released] = "已公布",
        [CalendarEventStatus.PendingReview] = "待复核",
        [CalendarEventStatus.Delayed] = "延迟",
        [CalendarEventStatus.Unconfirmed] = "待确认",
        [CalendarEventStatus.Cancelled] = "取消",

        [EventOrigin.Collected] = "排期采集",
        [EventOrigin.Supplemented] = "补建",

        [TimePrecision.Minute] = "精确到分钟",
        [TimePrecision.DateOnly] = "仅日期",
        [TimePrecision.ToBeDetermined] = "待定",

        [LocalTimeResolution.Exact] = "正常",
        [LocalTimeResolution.ShiftedForward] = "不存在的时刻，已按跳过的时长顺延",
        [LocalTimeResolution.Ambiguous] = "重复出现的时刻，已取较早的一次",

        [EventField.Actual] = "公布值",
        [EventField.Forecast] = "预期值",
        [EventField.Previous] = "前值",
        [EventField.PreviousRevised] = "前值修正",
        [EventField.Status] = "状态",

        [EventChangeKind.Rescheduled] = "改期",
        [EventChangeKind.Status] = "状态",
        [EventChangeKind.Actual] = "公布值",
        [EventChangeKind.Previous] = "前值",
        [EventChangeKind.PreviousRevised] = "前值修正",
        [EventChangeKind.RevisedValue] = "修正后数值",
        [EventChangeKind.Forecast] = "预期值",
        [EventChangeKind.Locked] = "锁定",
        [EventChangeKind.Unlocked] = "解锁",

        [ChangeActorKind.Collector] = "采集",
        [ChangeActorKind.User] = "人工",

        [LockConflictStatus.Pending] = "待处理",
        [LockConflictStatus.Resolved] = "已处理",

        [LockConflictResolution.KeepLocked] = "保持锁定",
        [LockConflictResolution.AdoptCollected] = "解锁并采用采集值",
        [LockConflictResolution.Unlocked] = "已解锁",

        [ForecastType.Manual] = "人工录入",
    };

    /// <summary>取枚举值的中文显示名；没有登记的值显示枚举名</summary>
    public static string Get(Enum value) => Map.TryGetValue(value, out var name) ? name : value.ToString();

    /// <summary>是否已登记（测试用来检查每个枚举值都有显示名）</summary>
    public static bool Contains(Enum value) => Map.ContainsKey(value);
}
