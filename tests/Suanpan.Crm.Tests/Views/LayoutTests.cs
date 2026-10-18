using System.Text.RegularExpressions;
using Suanpan.Crm.Tests.Fixtures;
using Suanpan.Services.Sys.Rbac.Models;
using Xunit;

namespace Suanpan.Crm.Tests.Views;

/// <summary>布局（sys 设计 §9.3、前端约定 §3.1）。用开发环境的示例页承载布局，它允许匿名访问</summary>
public partial class LayoutTests
{
    private const string Page = "/Dev/Components";

    /// <summary>完成标准：所有静态资源自托管，页面不请求任何外部地址；校验脚本先 jquery-validation 后 unobtrusive</summary>
    [Fact]
    public async Task LoadsOnlyLocalResources_InRequiredOrder()
    {
        await using var app = new CrmApp("Development");

        var html = await app.CreateBrowser().GetStringAsync(Page, Ct);

        var urls = ResourceUrl().Matches(html).Select(m => m.Groups["url"].Value).ToList();
        Assert.NotEmpty(urls);
        Assert.All(urls, url => Assert.StartsWith("/", url));
        Assert.DoesNotContain(urls, url => url.StartsWith("//", StringComparison.Ordinal));

        var scripts = new[] { "/lib/jquery/jquery.min.js", "/lib/htmx/htmx.min.js", "/js/site.js",
            "/lib/jquery-validation/jquery.validate.min.js", "/lib/jquery-validation-unobtrusive/jquery.validate.unobtrusive.min.js" };
        var positions = scripts.Select(script => html.IndexOf(script, StringComparison.Ordinal)).ToList();
        Assert.DoesNotContain(-1, positions);
        Assert.Equal(positions.Order(), positions);
    }

    /// <summary>HTMX 请求通过 body 上的 hx-headers 带防伪令牌（前端约定 §4.2）</summary>
    [Fact]
    public async Task BodyCarriesAntiforgeryHeaderForHtmx()
    {
        await using var app = new CrmApp("Development");

        var html = await app.CreateBrowser().GetStringAsync(Page, Ct);

        Assert.Matches("<body hx-headers=\"{&quot;RequestVerificationToken&quot;:&quot;[^&]+&quot;}\"", html);
    }

    /// <summary>菜单由权限数据驱动：显示当前账号的菜单树，当前页面所在的菜单高亮</summary>
    [Fact]
    public async Task ShowsMenusOfCurrentUser()
    {
        await using var app = new CrmApp("Development");
        app.Menus.Set(7, new MenuNode(1, "系统管理", null, "settings",
        [
            new MenuNode(2, "账号管理", "/Sys/User", "users", []),
            new MenuNode(3, "示例", Page, null, []),
        ]));

        var html = await app.CreateBrowser().GetStringAsync(Page, app.CreateCookie(7), Ct);

        Assert.Contains("href=\"/Sys/User\"", html);
        Assert.Matches("class=\"dropdown-item active\" href=\"/Dev/Components\"", html);
        Assert.DoesNotMatch("class=\"dropdown-item active\" href=\"/Sys/User\"", html);
    }

    [GeneratedRegex("<(?:script|link)[^>]+(?:src|href)=\"(?<url>[^\"]+)\"")]
    private static partial Regex ResourceUrl();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
