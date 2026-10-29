namespace Suanpan.Core.Calendar.Enums;

/// <summary>可锁定的字段（BR-060，日历设计 §3.3）；排期时间、时间精度不可锁定（DEC-024）</summary>
public enum EventField : short
{
    /// <summary>公布值（连同上限）</summary>
    Actual = 1,

    /// <summary>预期值（连同上限）</summary>
    Forecast = 2,

    /// <summary>前值（连同上限）</summary>
    Previous = 3,

    /// <summary>前值修正</summary>
    PreviousRevised = 4,

    /// <summary>状态</summary>
    Status = 5,
}
