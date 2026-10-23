namespace Suanpan.Core.Reference.Enums;

/// <summary>指标类别（基础数据设计 §3.6）；中英文显示名见 ReferenceDisplayNames</summary>
public enum IndicatorCategory : short
{
    /// <summary>就业</summary>
    Employment = 1,

    /// <summary>通胀</summary>
    Inflation = 2,

    /// <summary>增长</summary>
    Growth = 3,

    /// <summary>消费</summary>
    Consumption = 4,

    /// <summary>贸易</summary>
    Trade = 5,

    /// <summary>利率</summary>
    InterestRate = 6,

    /// <summary>景气（PMI、信心指数等调查类指标）</summary>
    Sentiment = 7,

    /// <summary>其他</summary>
    Other = 8,
}
