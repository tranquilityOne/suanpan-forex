using Suanpan.Core.Collect.Enums;

namespace Suanpan.Services.Collect.Sources.Models;

/// <summary>
/// 新增或修改数据源（采集设计 §3.1）。Credential 为新输入的凭据明文：为空表示不改变已有凭据，ClearCredential 为 true 时清除。
/// 启用状态不在这里改，见 SetEnabledAsync
/// </summary>
public sealed record DataSourceInput
{
    public required string Name { get; init; }

    public required string Agency { get; init; }

    public string? EconomyCode { get; init; }

    public required DataSourceType Type { get; init; }

    public required string BaseUrl { get; init; }

    public required int RateLimit { get; init; }

    public required RateLimitUnit RateLimitUnit { get; init; }

    public required short TimeoutSeconds { get; init; }

    /// <summary>每日请求上限（BR-003）；为空表示不限</summary>
    public int? DailyLimit { get; init; }

    /// <summary>盯盘请求间隔（秒，BR-014）；为空时用系统配置的默认值</summary>
    public short? WatchIntervalSeconds { get; init; }

    public required bool CredentialRequired { get; init; }

    public string? Credential { get; init; }

    public bool ClearCredential { get; init; }

    public required ComplianceClass ComplianceClass { get; init; }

    public required string TermsUrl { get; init; }

    public required RobotsResult RobotsResult { get; init; }

    public string? Remark { get; init; }
}
