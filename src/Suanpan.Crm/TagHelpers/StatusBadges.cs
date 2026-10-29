using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Rules;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Core.Sys.Enums;

namespace Suanpan.Crm.TagHelpers;

/// <summary>
/// 各模块状态枚举的显示文字和颜色，供 &lt;status-badge&gt; 使用。新增需要显示为徽章的枚举时在这里登记，
/// 枚举的每个值都要有对应项（测试会检查），颜色按前端约定 §7.1 的语义选择。
/// </summary>
public static class StatusBadges
{
    private static readonly Dictionary<Enum, (BadgeTone Tone, string Text)> Map = new()
    {
        // Sys
        [UserStatus.Pending] = (BadgeTone.Attention, "待激活"),
        [UserStatus.Active] = (BadgeTone.Success, "启用"),
        [UserStatus.Disabled] = (BadgeTone.Neutral, "停用"),

        // Collect：运行记录的状态（采集设计 §3.3），文字与 CollectDisplayNames 一致
        [CollectRunStatus.Queued] = (BadgeTone.Neutral, CollectDisplayNames.Get(CollectRunStatus.Queued)),
        [CollectRunStatus.Running] = (BadgeTone.InProgress, CollectDisplayNames.Get(CollectRunStatus.Running)),
        [CollectRunStatus.Succeeded] = (BadgeTone.Success, CollectDisplayNames.Get(CollectRunStatus.Succeeded)),
        [CollectRunStatus.PartiallySucceeded] = (BadgeTone.Attention, CollectDisplayNames.Get(CollectRunStatus.PartiallySucceeded)),
        [CollectRunStatus.Failed] = (BadgeTone.Failure, CollectDisplayNames.Get(CollectRunStatus.Failed)),
        [CollectRunStatus.Skipped] = (BadgeTone.Neutral, CollectDisplayNames.Get(CollectRunStatus.Skipped)),

        // Calendar：日历事件的状态（日历设计 §3.2），文字与 CalendarDisplayNames 一致
        [CalendarEventStatus.Scheduled] = (BadgeTone.Neutral, CalendarDisplayNames.Get(CalendarEventStatus.Scheduled)),
        [CalendarEventStatus.Watching] = (BadgeTone.InProgress, CalendarDisplayNames.Get(CalendarEventStatus.Watching)),
        [CalendarEventStatus.Released] = (BadgeTone.Success, CalendarDisplayNames.Get(CalendarEventStatus.Released)),
        [CalendarEventStatus.PendingReview] = (BadgeTone.Attention, CalendarDisplayNames.Get(CalendarEventStatus.PendingReview)),
        [CalendarEventStatus.Delayed] = (BadgeTone.Abnormal, CalendarDisplayNames.Get(CalendarEventStatus.Delayed)),
        [CalendarEventStatus.Unconfirmed] = (BadgeTone.Attention, CalendarDisplayNames.Get(CalendarEventStatus.Unconfirmed)),
        [CalendarEventStatus.Cancelled] = (BadgeTone.Neutral, CalendarDisplayNames.Get(CalendarEventStatus.Cancelled)),
    };

    /// <summary>已登记的枚举类型</summary>
    public static IEnumerable<Type> EnumTypes => Map.Keys.Select(value => value.GetType()).Distinct();

    /// <summary>取枚举值的颜色和文字；没有登记的值显示为中性颜色和枚举名</summary>
    public static (BadgeTone Tone, string Text) Describe(Enum value)
        => Map.TryGetValue(value, out var badge) ? badge : (BadgeTone.Neutral, value.ToString());

    public static string CssClass(BadgeTone tone) => tone switch
    {
        BadgeTone.Success => "bg-green-lt",
        BadgeTone.InProgress => "bg-blue-lt",
        BadgeTone.Attention => "bg-yellow-lt",
        BadgeTone.Abnormal => "bg-orange-lt",
        BadgeTone.Failure => "bg-red-lt",
        _ => "bg-secondary-lt",
    };
}
