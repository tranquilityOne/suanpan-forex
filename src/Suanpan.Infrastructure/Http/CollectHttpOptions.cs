namespace Suanpan.Infrastructure.Http;

/// <summary>采集请求的设置（配置节 Collect，采集设计 §5.4、§10）</summary>
public sealed class CollectHttpOptions
{
    public const string SectionName = "Collect";

    /// <summary>User-Agent 中的产品标识</summary>
    public const string Product = "SuanpanForexBot/1.0";

    /// <summary>
    /// User-Agent 中的联系邮箱（PRD BR-005，采集设计 §11 第 7 项）。不涉密，但随部署配置，不写在代码里：
    /// 本地用 user-secrets，其他环境用环境变量 SUANPAN__Collect__ContactEmail
    /// </summary>
    public string? ContactEmail { get; set; }

    /// <summary>采集请求统一使用的 User-Agent：<c>SuanpanForexBot/1.0 (+mailto:联系邮箱)</c>；没配置邮箱时只有产品标识</summary>
    public string UserAgent => string.IsNullOrWhiteSpace(ContactEmail) ? Product : $"{Product} (+mailto:{ContactEmail.Trim()})";
}
