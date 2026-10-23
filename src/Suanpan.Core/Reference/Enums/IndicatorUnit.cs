namespace Suanpan.Core.Reference.Enums;

/// <summary>
/// 指标单位，决定公布值的存储口径（基础数据设计 §3.6，BR-043）：解析时原始单位与字典不一致即解析失败。
/// 清单按首批指标整理（JAN-78 编辑确认时一并确认），有新单位时在这里增加枚举值，已有的值不能改。
/// </summary>
public enum IndicatorUnit : short
{
    /// <summary>百分比（%）</summary>
    Percent = 1,

    /// <summary>指数，不带单位</summary>
    Index = 2,

    /// <summary>人</summary>
    Persons = 3,

    /// <summary>千人</summary>
    ThousandPersons = 4,

    /// <summary>亿美元</summary>
    HundredMillionUsd = 5,
}
