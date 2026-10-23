using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Core.Common.Auditing;

namespace Suanpan.Core.Collect.Entities;

/// <summary>数据源（col_source，采集设计 §2.1）；保存时的校验见 DataSourceRule</summary>
public class DataSource : AuditedEntity
{
    public long Id { get; set; }

    /// <summary>名称，唯一</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>所属机构，如"美国劳工统计局"</summary>
    public string Agency { get; set; } = string.Empty;

    /// <summary>所属经济体，可不填（如跨经济体的数据门户）</summary>
    public string? EconomyCode { get; set; }

    public DataSourceType Type { get; set; }

    /// <summary>http 或 https 地址；连通性测试请求这个地址</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>请求频率上限（BR-003），单位见 RateLimitUnit；所有任务和盯盘对同一数据源的请求合计不超过它</summary>
    public int RateLimit { get; set; }

    public RateLimitUnit RateLimitUnit { get; set; }

    /// <summary>单次请求超时（秒）</summary>
    public short TimeoutSeconds { get; set; } = DataSourceRule.DefaultTimeoutSeconds;

    /// <summary>为 true 时必须有凭据（ERR-SRC-004）</summary>
    public bool CredentialRequired { get; set; }

    /// <summary>凭据密文（ISecretProtector 加密，enc: 开头，BR-004）；只在发请求时解密，不显示、不写日志</summary>
    public string? Credential { get; set; }

    public ComplianceClass ComplianceClass { get; set; }

    /// <summary>条款链接</summary>
    public string TermsUrl { get; set; } = string.Empty;

    public RobotsResult RobotsResult { get; set; }

    public string? Remark { get; set; }

    /// <summary>新建时为停用（FR-001 验收第 1 条）；启用前须通过合规检查（BR-002）</summary>
    public bool IsEnabled { get; set; }
}
