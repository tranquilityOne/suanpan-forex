using Suanpan.Core.Common.Localization;
using Suanpan.Core.Reference.Entities;
using Suanpan.Core.Reference.Enums;
using Suanpan.Core.Sys.Enums;
using Suanpan.Crm.Tests.Fixtures;
using Xunit;
using static Suanpan.Crm.Tests.Fixtures.BrowserForms;

namespace Suanpan.Crm.Tests.Controllers;

/// <summary>
/// FR-006 验收第 2 条：修改影响方向时填写的理由记入操作日志的原因列（BR-031，基础数据设计 §3.4）。
/// 指标服务用替身；方向变化时必须填理由的校验在 Suanpan.Services.Tests 中测试。
/// </summary>
public class IndicatorControllerTests
{
    [Fact]
    public async Task Edit_RecordsReasonInOpLog()
    {
        await using var app = new CrmApp();
        app.Auth.AddAccount(11, SignInStep.VerifyTotp);
        app.Permissions.Grant(11, "reference:indicator:edit");
        app.Indicators.Indicators["US.NFP"] = Nfp();
        var browser = await SignedInBrowserAsync(app, "user11");

        var response = await SubmitAsync(browser, "/", new()
        {
            ["CurrencyCode"] = "USD",
            ["NameZh"] = "非农就业人数",
            ["NameEn"] = "Nonfarm Payrolls",
            ["ShortNameZh"] = "非农",
            ["ShortNameEn"] = "NFP",
            ["Category"] = nameof(IndicatorCategory.Employment),
            ["ValueType"] = nameof(IndicatorValueType.Value),
            ["Unit"] = nameof(IndicatorUnit.ThousandPersons),
            ["Decimals"] = "0",
            ["Frequency"] = nameof(ReleaseFrequency.Monthly),
            ["Importance"] = nameof(Importance.High),
            ["Direction"] = nameof(ImpactDirection.HigherIsBearish),
            ["FlatTolerance"] = "10",
            ["AgencyZh"] = "美国劳工统计局",
            ["AgencyEn"] = "Bureau of Labor Statistics",
            ["DefaultReleaseTime"] = "08:30",
            ["Reason"] = "统计口径调整",
        }, action: "/Reference/Indicator/Edit/US.NFP");

        Assert.Equal("/Reference/Indicator/Detail/US.NFP", response.RedirectPath());
        var input = Assert.Single(app.Indicators.Updated);
        Assert.Equal(("统计口径调整", ImpactDirection.HigherIsBearish), (input.Reason, input.Direction));
        var log = Assert.Single(app.OpLogs.Written, l => l.Action == "编辑指标");
        Assert.Equal(("统计口径调整", "reference:indicator:edit", true), (log.Reason, log.PermCode, log.IsSuccess));
    }

    private static Indicator Nfp() => new()
    {
        Code = "US.NFP",
        EconomyCode = "US",
        CurrencyCode = "USD",
        Name = new BilingualText("非农就业人数", "Nonfarm Payrolls"),
        ShortName = new BilingualText("非农", "NFP"),
        Agency = new BilingualText("美国劳工统计局", "Bureau of Labor Statistics"),
        Direction = ImpactDirection.HigherIsBullish,
    };
}
