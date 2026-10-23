namespace Suanpan.Core.Collect.Enums;

/// <summary>robots.txt 检查结果（BR-002）："不允许"的数据源不能启用</summary>
public enum RobotsResult : short
{
    /// <summary>允许</summary>
    Allowed = 1,

    /// <summary>不涉及（API）</summary>
    NotApplicable = 2,

    /// <summary>不允许</summary>
    Disallowed = 3,
}
