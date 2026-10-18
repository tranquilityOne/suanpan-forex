using System.Net;
using Suanpan.Crm.Tests.Fixtures;
using Xunit;

namespace Suanpan.Crm.Tests.Controllers;

/// <summary>公共部件示例页：只在开发环境可用；页面上的 _Pager、&lt;confirm-form&gt; 按约定输出</summary>
public class DevComponentsControllerTests
{
    [Fact]
    public async Task NotAvailableOutsideDevelopment()
    {
        await using var app = new CrmApp();

        var response = await app.CreateBrowser().SendAsync(Requests.Get("/Dev/Components"), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>分页链接保留全部筛选参数，只替换 page</summary>
    [Fact]
    public async Task PagerKeepsFilters()
    {
        await using var app = new CrmApp("Development");

        var html = await app.CreateBrowser().GetStringAsync("/Dev/Components?keyword=abc&page=2", Ct);

        Assert.Contains("href=\"/Dev/Components?keyword=abc&amp;page=1\"", html);
        Assert.Contains("href=\"/Dev/Components?keyword=abc&amp;page=3\"", html);
    }

    /// <summary>&lt;confirm-form&gt;：POST 到指定地址，带防伪令牌和确认提示</summary>
    [Fact]
    public async Task ConfirmFormPostsWithAntiforgeryToken()
    {
        await using var app = new CrmApp("Development");

        var html = await app.CreateBrowser().GetStringAsync("/Dev/Components", Ct);

        Assert.Matches(
            "<form method=\"post\" action=\"/Dev/Components/Disable/1\" class=\"d-inline\" data-confirm=\"[^\"]+\" data-confirm-button=\"[^\"]+\">"
            + "<input name=\"__RequestVerificationToken\" type=\"hidden\" value=\"[^\"]+\" />",
            html);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
