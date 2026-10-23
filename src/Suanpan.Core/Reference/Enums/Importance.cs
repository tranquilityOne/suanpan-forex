namespace Suanpan.Core.Reference.Enums;

/// <summary>指标的重要性（BR-035）：数值越大越重要，可以按大小比较</summary>
public enum Importance : short
{
    /// <summary>低</summary>
    Low = 1,

    /// <summary>中</summary>
    Medium = 2,

    /// <summary>高：进入盯盘计划（FR-003）</summary>
    High = 3,
}
