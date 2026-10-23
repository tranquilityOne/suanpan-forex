namespace Suanpan.Core.Reference.Enums;

/// <summary>指标的数值类型（BR-032）</summary>
public enum IndicatorValueType : short
{
    /// <summary>单个数值</summary>
    Value = 1,

    /// <summary>区间：日历事件同时存上下限，如联邦基金利率目标区间</summary>
    Range = 2,
}
