using Suanpan.Core.Collect.Enums;

namespace Suanpan.Services.Collect.Sources.Models;

/// <summary>数据源列表的一行；凭据只给出是否已设置</summary>
public sealed record DataSourceRow(
    long Id,
    string Name,
    string Agency,
    string? EconomyCode,
    DataSourceType Type,
    int RateLimit,
    RateLimitUnit RateLimitUnit,
    ComplianceClass ComplianceClass,
    RobotsResult RobotsResult,
    bool CredentialRequired,
    bool HasCredential,
    bool IsEnabled);
