namespace Suanpan.Core.Calendar.Enums;

/// <summary>排期时间的精度（BR-038）；只有精确到分钟的事件进入盯盘（BR-018）</summary>
public enum TimePrecision : short
{
    /// <summary>精确到分钟</summary>
    Minute = 1,

    /// <summary>仅日期</summary>
    DateOnly = 2,

    /// <summary>待定</summary>
    ToBeDetermined = 3,
}
