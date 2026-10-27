namespace Suanpan.Core.Flash.Enums;

/// <summary>
/// 数据公布快讯的结论（BR-074、BR-075），由 FlashConclusionRule 判定；对应的中英文文案作为模板维护（BR-071）
/// </summary>
public enum FlashConclusion : short
{
    /// <summary>利多本币</summary>
    Bullish = 1,

    /// <summary>利空本币</summary>
    Bearish = 2,

    /// <summary>符合预期：与预期值的偏差在持平容差内</summary>
    InLine = 3,

    /// <summary>较前值上升（没有预期值时）</summary>
    AbovePrevious = 4,

    /// <summary>较前值下降（没有预期值时）</summary>
    BelowPrevious = 5,

    /// <summary>较前值持平（没有预期值时，与前值的差在持平容差内）</summary>
    FlatWithPrevious = 6,

    /// <summary>无结论：无方向的指标偏离预期，或者既没有预期值也没有前值</summary>
    None = 7,
}
