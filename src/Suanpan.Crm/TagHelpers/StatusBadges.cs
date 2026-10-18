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
