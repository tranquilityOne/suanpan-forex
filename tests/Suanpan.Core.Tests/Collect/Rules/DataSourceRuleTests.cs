using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Xunit;

namespace Suanpan.Core.Tests.Collect.Rules;

public class DataSourceRuleTests
{
    [Fact]
    public void Validate_AcceptsCompleteSource()
        => Assert.Null(DataSourceRule.Validate(Valid()));

    /// <summary>不需要凭据时可以没有凭据；超时取边界值</summary>
    [Theory]
    [InlineData(DataSourceRule.MinTimeoutSeconds)]
    [InlineData(DataSourceRule.MaxTimeoutSeconds)]
    public void Validate_AcceptsBoundaryTimeoutWithoutCredential(short timeoutSeconds)
    {
        var source = Valid();
        (source.CredentialRequired, source.Credential, source.TimeoutSeconds) = (false, null, timeoutSeconds);

        Assert.Null(DataSourceRule.Validate(source));
    }

    /// <summary>必填、地址格式、枚举、频率上限、超时、凭据；Field 指向出错的表单字段</summary>
    [Theory]
    [MemberData(nameof(InvalidCases))]
    public void Validate_ReportsTheInvalidField(string field, Action<DataSource> breakIt)
    {
        var source = Valid();
        breakIt(source);

        Assert.Equal(field, DataSourceRule.Validate(source)?.Field);
    }

    public static TheoryData<string, Action<DataSource>> InvalidCases => new()
    {
        { "Name", s => s.Name = " " },
        { "Agency", s => s.Agency = "" },
        { "Type", s => s.Type = default },
        { "BaseUrl", s => s.BaseUrl = "" },
        { "BaseUrl", s => s.BaseUrl = "ftp://data.bls.gov" },
        { "BaseUrl", s => s.BaseUrl = "data.bls.gov/schedule" },     // 不是绝对地址
        { "TermsUrl", s => s.TermsUrl = "javascript:alert(1)" },
        { "RateLimitUnit", s => s.RateLimitUnit = (RateLimitUnit)9 },
        { "ComplianceClass", s => s.ComplianceClass = default },
        { "RobotsResult", s => s.RobotsResult = default },
        { "RateLimit", s => s.RateLimit = 0 },
        { "TimeoutSeconds", s => s.TimeoutSeconds = DataSourceRule.MaxTimeoutSeconds + 1 },
        { "TimeoutSeconds", s => s.TimeoutSeconds = 0 },
        { "DailyLimit", s => s.DailyLimit = 0 },                         // BR-003：不限时留空，不能填 0
        { "WatchIntervalSeconds", s => s.WatchIntervalSeconds = 0 },
        { "WatchIntervalSeconds", s => s.WatchIntervalSeconds = WatchRule.MaxIntervalSeconds + 1 },
        { "Credential", s => s.Credential = null },                   // ERR-SRC-004：需要凭据但没有
    };

    /// <summary>BR-003：每日请求数用到上限的 90% 时告警，向上取整，上限很小时就是上限本身</summary>
    [Theory]
    [InlineData(500, 450)]
    [InlineData(25, 23)]
    [InlineData(1, 1)]
    public void DailyLimitAlertAt_NinetyPercentRoundedUp(int limit, long expected)
        => Assert.Equal(expected, DataSourceRule.DailyLimitAlertAt(limit));

    /// <summary>BR-002、ERR-SRC-001：合规类别为"待确认"或 robots.txt 为"不允许"时不能启用</summary>
    [Theory]
    [InlineData(ComplianceClass.Pending, RobotsResult.Allowed, "ComplianceClass")]
    [InlineData(ComplianceClass.GovernmentPublic, RobotsResult.Disallowed, "RobotsResult")]
    public void CanEnable_RejectsUnconfirmedCompliance(ComplianceClass compliance, RobotsResult robots, string field)
    {
        var source = Valid();
        (source.ComplianceClass, source.RobotsResult) = (compliance, robots);

        Assert.Equal(field, DataSourceRule.ValidateCanEnable(source)?.Field);
    }

    [Theory]
    [InlineData(ComplianceClass.GovernmentPublic, RobotsResult.Allowed)]
    [InlineData(ComplianceClass.InstitutionLicense, RobotsResult.NotApplicable)]
    public void CanEnable_AcceptsConfirmedCompliance(ComplianceClass compliance, RobotsResult robots)
    {
        var source = Valid();
        (source.ComplianceClass, source.RobotsResult) = (compliance, robots);

        Assert.Null(DataSourceRule.ValidateCanEnable(source));
    }

    /// <summary>停用的数据源可以保存为"待确认"；已启用的不行，否则绕过了启用时的检查</summary>
    [Fact]
    public void Validate_RejectsUnconfirmedComplianceOnlyWhenEnabled()
    {
        var source = Valid();
        source.ComplianceClass = ComplianceClass.Pending;

        Assert.Null(DataSourceRule.Validate(source));
        source.IsEnabled = true;
        Assert.Equal("ComplianceClass", DataSourceRule.Validate(source)?.Field);
    }

    private static DataSource Valid() => new()
    {
        Name = "BLS 官网",
        Agency = "美国劳工统计局",
        EconomyCode = "US",
        Type = DataSourceType.OfficialApi,
        BaseUrl = "https://api.bls.gov/publicAPI/v2/",
        RateLimit = 2,
        RateLimitUnit = RateLimitUnit.PerSecond,
        CredentialRequired = true,
        Credential = "enc:ciphertext",
        ComplianceClass = ComplianceClass.GovernmentPublic,
        TermsUrl = "https://www.bls.gov/bls/linksite.htm",
        RobotsResult = RobotsResult.NotApplicable,
    };
}
