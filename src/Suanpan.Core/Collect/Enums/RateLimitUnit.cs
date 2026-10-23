namespace Suanpan.Core.Collect.Enums;

/// <summary>数据源请求频率上限的单位（BR-003）</summary>
public enum RateLimitUnit : short
{
    /// <summary>次/秒</summary>
    PerSecond = 1,

    /// <summary>次/分</summary>
    PerMinute = 2,
}
