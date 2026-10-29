using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Sys.Enums;
using Suanpan.Crm.Tests.Fixtures;
using Suanpan.Services.Calendar.Unmatched.Models;
using Xunit;
using static Suanpan.Crm.Tests.Fixtures.BrowserForms;

namespace Suanpan.Crm.Tests.Controllers;

/// <summary>
/// 未匹配条目（日历设计 §9.1）："新增映射"预填数据源、用途"排期"和发布标识；按发布标识忽略记入操作日志。
/// 未匹配条目服务用替身；忽略、重新匹配的数据库行为在 Suanpan.Services.Tests 中测试
/// </summary>
public class UnmatchedControllerTests
{
    [Fact]
    public async Task Index_LinksToScheduleMappingPrefilled()
    {
        await using var app = new CrmApp();
        var browser = await SignedInAsync(app, "calendar:unmatched:index", "reference:indicator_source:create");

        var html = await browser.GetStringAsync("/Calendar/Unmatched", TestContext.Current.CancellationToken);

        Assert.Contains("href=\"/Reference/IndicatorSource/Create?sourceId=7&amp;purpose=Schedule&amp;externalId=prod\"", html);
    }

    [Fact]
    public async Task Ignore_PassesReleaseKeyAndLogs()
    {
        await using var app = new CrmApp();
        var browser = await SignedInAsync(app, "calendar:unmatched:index", "calendar:unmatched:ignore");

        var response = await SubmitAsync(browser, "/Calendar/Unmatched", new(), action: "/Calendar/Unmatched/Ignore?sourceId=7&releaseKey=prod");

        Assert.Equal("/Calendar/Unmatched", response.RedirectPath());
        Assert.Equal((7L, "prod"), Assert.Single(app.Unmatched.Ignored));
        var log = Assert.Single(app.OpLogs.Written, l => l.Action == "忽略发布");
        Assert.Equal(("calendar:unmatched:ignore", true), (log.PermCode, log.IsSuccess));
    }

    private static async Task<HttpClient> SignedInAsync(CrmApp app, params string[] permCodes)
    {
        app.Auth.AddAccount(22, SignInStep.VerifyTotp);
        app.Permissions.Grant(22, permCodes);
        app.Unmatched.Groups.Add(new UnmatchedReleaseGroup(7, "BLS", "America/New_York", "prod", "Productivity and Costs",
            new DateTime(2026, 10, 29, 0, 0, 0, DateTimeKind.Utc),
            [new UnmatchedEntryRow("2026-Q3", new DateOnly(2026, 11, 5), new TimeOnly(8, 30), TimePrecision.Minute)]));
        return await SignedInBrowserAsync(app, "user22");
    }
}
