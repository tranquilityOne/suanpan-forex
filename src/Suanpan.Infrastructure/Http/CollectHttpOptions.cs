namespace Suanpan.Infrastructure.Http;

/// <summary>采集请求的设置（配置节 Collect，采集设计 §5.4、§10）</summary>
public sealed class CollectHttpOptions
{
    public const string SectionName = "Collect";

    /// <summary>
    /// 采集请求统一使用的 User-Agent，带项目名和联系邮箱（PRD BR-005），如 "SuanpanForexBot/1.0 (+mailto:联系邮箱)"。
    /// 不涉密，可写在 appsettings.json；联系邮箱确定之前用不带邮箱的默认值
    /// </summary>
    public string UserAgent { get; set; } = "SuanpanForexBot/1.0";
}
