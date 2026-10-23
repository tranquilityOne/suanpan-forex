using Suanpan.Core.Common.Localization;
using Suanpan.Core.Reference.Enums;

namespace Suanpan.Core.Reference.Rules;

/// <summary>
/// 基础数据枚举的中英文显示名（基础数据设计 §3.6），后台和快讯共用。
/// 枚举增加取值时在这里登记，每个值都要有对应项（测试会检查）。
/// </summary>
public static class ReferenceDisplayNames
{
    private static readonly Dictionary<Enum, BilingualText> Map = new()
    {
        [IndicatorCategory.Employment] = new("就业", "Employment"),
        [IndicatorCategory.Inflation] = new("通胀", "Inflation"),
        [IndicatorCategory.Growth] = new("增长", "Growth"),
        [IndicatorCategory.Consumption] = new("消费", "Consumption"),
        [IndicatorCategory.Trade] = new("贸易", "Trade"),
        [IndicatorCategory.InterestRate] = new("利率", "Interest Rate"),
        [IndicatorCategory.Sentiment] = new("景气", "Sentiment"),
        [IndicatorCategory.Other] = new("其他", "Other"),

        [IndicatorValueType.Value] = new("数值", "Value"),
        [IndicatorValueType.Range] = new("区间", "Range"),

        [IndicatorUnit.Percent] = new("百分比（%）", "Percent (%)"),
        [IndicatorUnit.Index] = new("指数", "Index"),
        [IndicatorUnit.Persons] = new("人", "Persons"),
        [IndicatorUnit.ThousandPersons] = new("千人", "Thousand Persons"),
        [IndicatorUnit.HundredMillionUsd] = new("亿美元", "USD 100 Million"),
        [IndicatorUnit.ThousandUnits] = new("千个", "Thousand Units"),

        [ReleaseFrequency.Weekly] = new("周", "Weekly"),
        [ReleaseFrequency.Monthly] = new("月", "Monthly"),
        [ReleaseFrequency.Quarterly] = new("季", "Quarterly"),
        [ReleaseFrequency.Irregular] = new("不定期", "Irregular"),

        [Importance.High] = new("高", "High"),
        [Importance.Medium] = new("中", "Medium"),
        [Importance.Low] = new("低", "Low"),

        [ImpactDirection.HigherIsBullish] = new("越高越利好本币", "Higher is bullish for the currency"),
        [ImpactDirection.HigherIsBearish] = new("越高越利空本币", "Higher is bearish for the currency"),
        [ImpactDirection.Neutral] = new("无方向", "No direction"),
    };

    /// <summary>取枚举值的中英文显示名；没有登记的值两种语言都显示枚举名</summary>
    public static BilingualText Get(Enum value)
        => Map.TryGetValue(value, out var name) ? name : new BilingualText(value.ToString(), value.ToString());

    /// <summary>是否已登记（测试用来检查每个枚举值都有显示名）</summary>
    public static bool Contains(Enum value) => Map.ContainsKey(value);
}
