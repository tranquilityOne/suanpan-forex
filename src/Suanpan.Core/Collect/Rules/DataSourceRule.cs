using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Common.Validation;

namespace Suanpan.Core.Collect.Rules;

/// <summary>
/// 数据源保存和启用时的校验（采集设计 §3.1，BR-001 ~ BR-004）。名称是否重复、经济体是否存在要查库，由服务校验。
/// </summary>
public static class DataSourceRule
{
    /// <summary>请求超时的默认值（PRD FR-002 异常流程"请求失败"）</summary>
    public const short DefaultTimeoutSeconds = 30;

    public const short MinTimeoutSeconds = 1;

    public const short MaxTimeoutSeconds = 120;

    /// <summary>
    /// 新增和修改共用：名称、机构必填；基础地址和条款链接是 http(s) 绝对地址；枚举是已定义的值；频率上限为正整数；
    /// 超时在范围内；需要凭据时凭据不能为空（ERR-SRC-004）；已启用的数据源合规信息必须仍然允许启用。
    /// 凭据按保存后的值判断：调用方先把"留空不改""清除"处理成最终的密文再校验。返回第一条错误，全部通过时返回 null
    /// </summary>
    public static FieldError? Validate(DataSource source)
    {
        var error = RequiredText(source.Name, nameof(DataSource.Name), "名称")
            ?? RequiredText(source.Agency, nameof(DataSource.Agency), "所属机构")
            ?? RequiredEnum(source.Type, nameof(DataSource.Type), "类型")
            ?? HttpUrl(source.BaseUrl, nameof(DataSource.BaseUrl), "基础地址")
            ?? RequiredEnum(source.RateLimitUnit, nameof(DataSource.RateLimitUnit), "频率单位")
            ?? RequiredEnum(source.ComplianceClass, nameof(DataSource.ComplianceClass), "合规类别")
            ?? HttpUrl(source.TermsUrl, nameof(DataSource.TermsUrl), "条款链接")
            ?? RequiredEnum(source.RobotsResult, nameof(DataSource.RobotsResult), "robots.txt 检查结果");
        if (error is not null)
        {
            return error;
        }

        if (source.RateLimit <= 0)
        {
            return new(nameof(DataSource.RateLimit), "请求频率上限必须是正整数");
        }

        if (source.TimeoutSeconds is < MinTimeoutSeconds or > MaxTimeoutSeconds)
        {
            return new(nameof(DataSource.TimeoutSeconds), $"请求超时必须在 {MinTimeoutSeconds} ~ {MaxTimeoutSeconds} 秒之间");
        }

        if (source.CredentialRequired && string.IsNullOrEmpty(source.Credential))
        {
            return new(nameof(DataSource.Credential), "这个数据源需要凭据，请填写");
        }

        // 已启用的数据源不能把合规信息改成不允许启用的值，否则绕过了启用时的检查
        return source.IsEnabled ? ValidateCanEnable(source) : null;
    }

    /// <summary>BR-002、ERR-SRC-001：合规类别为"待确认"或 robots.txt 为"不允许"时不能启用</summary>
    public static FieldError? ValidateCanEnable(DataSource source)
    {
        if (source.ComplianceClass == ComplianceClass.Pending)
        {
            return new(nameof(DataSource.ComplianceClass), "合规类别为\"待确认\"，请先完成合规确认");
        }

        return source.RobotsResult == RobotsResult.Disallowed
            ? new(nameof(DataSource.RobotsResult), "robots.txt 不允许采集，不能启用")
            : null;
    }

    /// <summary>频率上限的计数窗口（BR-003）：次/秒为 1 秒，次/分为 1 分钟</summary>
    public static TimeSpan RateLimitWindow(RateLimitUnit unit) => unit == RateLimitUnit.PerMinute ? TimeSpan.FromMinutes(1) : TimeSpan.FromSeconds(1);

    private static FieldError? RequiredText(string? value, string field, string label)
        => string.IsNullOrWhiteSpace(value) ? new(field, $"请填写{label}") : null;

    private static FieldError? RequiredEnum<TEnum>(TEnum value, string field, string label)
        where TEnum : struct, Enum
        => Enum.IsDefined(value) ? null : new(field, $"请选择{label}");

    private static FieldError? HttpUrl(string? value, string field, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new(field, $"请填写{label}");
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? null
            : new(field, $"{label}必须是以 http:// 或 https:// 开头的完整地址");
    }
}
