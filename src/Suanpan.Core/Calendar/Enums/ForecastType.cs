namespace Suanpan.Core.Calendar.Enums;

/// <summary>
/// 预期值的来源类型（BR-053、DEC-002）。第一阶段只启用人工录入；"多源采集""模型预估"启用时再加取值
/// </summary>
public enum ForecastType : short
{
    /// <summary>人工录入（市场共识）</summary>
    Manual = 1,
}
