namespace Suanpan.Core.Reference.Enums;

/// <summary>指标数值对本币的影响方向（BR-031）：快讯据此判断利多利空（FR-014）；修改时必须填写理由</summary>
public enum ImpactDirection : short
{
    /// <summary>数值越高越利好本币</summary>
    HigherIsBullish = 1,

    /// <summary>数值越高越利空本币</summary>
    HigherIsBearish = 2,

    /// <summary>无方向</summary>
    Neutral = 3,
}
