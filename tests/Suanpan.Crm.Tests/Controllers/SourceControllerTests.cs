using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Sys.Enums;
using Suanpan.Crm.Tests.Fixtures;
using Xunit;
using static Suanpan.Crm.Tests.Fixtures.BrowserForms;

namespace Suanpan.Crm.Tests.Controllers;

/// <summary>
/// FR-001 验收第 5 条：新增数据源记入操作日志，凭据脱敏、不记录明文（BR-004、sys 设计 §7.1）。
/// 数据源服务用替身；凭据加密在 Suanpan.Services.Tests 中测试。
/// </summary>
public class SourceControllerTests
{
    private const string Credential = "plain-api-key-123";

    [Fact]
    public async Task Create_LogsRequestWithMaskedCredential()
    {
        await using var app = new CrmApp();
        app.Auth.AddAccount(14, SignInStep.VerifyTotp);
        app.Permissions.Grant(14, "collect:source:create");
        var browser = await SignedInBrowserAsync(app, "user14");

        var response = await SubmitAsync(browser, "/", new()
        {
            ["Name"] = "BLS 官网",
            ["Agency"] = "美国劳工统计局",
            ["Type"] = nameof(DataSourceType.OfficialApi),
            ["BaseUrl"] = "https://api.bls.gov/publicAPI/v2/",
            ["RateLimit"] = "2",
            ["RateLimitUnit"] = nameof(RateLimitUnit.PerSecond),
            ["TimeoutSeconds"] = "30",
            ["CredentialRequired"] = "true",
            ["Credential"] = Credential,
            ["ComplianceClass"] = nameof(ComplianceClass.GovernmentPublic),
            ["TermsUrl"] = "https://www.bls.gov/bls/linksite.htm",
            ["RobotsResult"] = nameof(RobotsResult.NotApplicable),
        }, action: "/Collect/Source/Create");

        Assert.Equal("/Collect/Source", response.RedirectPath());
        Assert.Equal(Credential, Assert.Single(app.DataSources.Created).Credential);
        var log = Assert.Single(app.OpLogs.Written, l => l.Action == "新增数据源");
        Assert.Equal(("collect:source:create", true), (log.PermCode, log.IsSuccess));
        Assert.DoesNotContain(Credential, log.RequestParams);
        Assert.Contains("\"Credential\":\"***\"", log.RequestParams);
    }
}
