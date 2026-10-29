namespace Suanpan.Core.Reference.Enums;

/// <summary>指标与数据源映射的用途（BR-034，基础数据设计 §3.8）；同一指标对同一数据源每种用途最多一条</summary>
public enum IndicatorSourcePurpose : short
{
    /// <summary>公布值：外部标识如序列 ID，带优先级（1 为主源，2 起为备源）</summary>
    Release = 1,

    /// <summary>排期：外部标识是官方日程中的发布标识，同一份报告的几个指标共用一个；不设优先级</summary>
    Schedule = 2,
}
