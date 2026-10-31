using Suanpan.Core.Reference.Enums;

namespace Suanpan.Core.Calendar.Rules;

/// <summary>
/// 人工录入的数值格式（BR-058，ERR-FC-001，日历设计 §3.7）：公布值、前值、前值修正、预期值共用。
/// 单位按指标字典，页面在输入框旁显示单位，不做换算
/// </summary>
public static class EventValueRule
{
    /// <summary>numeric(18,6) 能存的整数部分上限</summary>
    private const decimal MaxMagnitude = 1_000_000_000_000m;

    /// <summary>
    /// 数值型不填上限；区间型必须填上限且不小于下限；小数位不超过指标的小数位（多出的不四舍五入，直接提示）。
    /// 通过时返回 null，否则返回提示
    /// </summary>
    public static string? Validate(decimal value, decimal? upper, IndicatorValueType valueType, short decimals)
    {
        if (valueType == IndicatorValueType.Range)
        {
            if (upper is not { } high)
            {
                return "区间型指标要填写上限";
            }

            if (high < value)
            {
                return "上限不能小于下限";
            }
        }
        else if (upper is not null)
        {
            return "数值型指标不填上限";
        }

        foreach (var number in upper is { } u ? [value, u] : new[] { value })
        {
            if (Math.Abs(number) >= MaxMagnitude)
            {
                return "数值太大";
            }

            if (Math.Round(number, decimals) != number)
            {
                return decimals == 0 ? "这个指标的值是整数，不能有小数" : $"这个指标最多 {decimals} 位小数";
            }
        }

        return null;
    }
}
