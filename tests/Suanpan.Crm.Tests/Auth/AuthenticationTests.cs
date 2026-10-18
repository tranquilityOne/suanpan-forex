using System.Net;
using Suanpan.Crm.Tests.Fixtures;
using Suanpan.Shared.Auth;
using Xunit;

namespace Suanpan.Crm.Tests.Auth;

/// <summary>完成标准：未登录访问任何页面都跳转到登录页（sys 设计 §5.2、§5.3）</summary>
public class AuthenticationTests
{
    private const long UserId = 7;

    [Theory]
    [InlineData("/RootProbe")]           // 根目录页面：兜底策略要求登录
    [InlineData("/Sys/PipelineProbe")]   // Area 页面：权限要求先要求登录
    public async Task Anonymous_IsRedirectedToLogin(string path)
    {
        await using var app = new CrmApp();

        var response = await app.CreateBrowser().SendAsync(Requests.Get(path), Ct);

        Assert.Equal("/Account/Login", response.RedirectPath());
    }

    /// <summary>FR-028 #1：只通过了密码、还没完成 TOTP 的待完成 Cookie 不能进入业务页面</summary>
    [Fact]
    public async Task PendingCookie_IsTreatedAsAnonymous()
    {
        await using var app = new CrmApp();
        app.Permissions.Grant(UserId, "sys:pipeline_probe:index");

        var response = await app.CreateBrowser().SendAsync(
            Requests.Get("/Sys/PipelineProbe", app.CreateCookie(UserId, AuthSchemes.CrmPending)), Ct);

        Assert.Equal("/Account/Login", response.RedirectPath());
    }

    /// <summary>改密码、重置、停用后安全戳变化，旧 Cookie 在下一个请求就失效</summary>
    [Fact]
    public async Task StaleSecurityStamp_IsSignedOut()
    {
        await using var app = new CrmApp();

        var response = await app.CreateBrowser().SendAsync(
            Requests.Get("/RootProbe", app.CreateCookie(UserId, stamp: "stamp-before-password-change")), Ct);

        Assert.Equal("/Account/Login", response.RedirectPath());
    }

    [Fact]
    public async Task LoggedIn_RootPageNeedsNoPermission()
    {
        await using var app = new CrmApp();

        var response = await app.CreateBrowser().SendAsync(Requests.Get("/RootProbe", app.CreateCookie(UserId)), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>HTMX 请求不跟随跳转：返回 401，用 HX-Redirect 让整页跳到登录页</summary>
    [Fact]
    public async Task AnonymousHtmxRequest_Gets401WithHxRedirect()
    {
        await using var app = new CrmApp();

        var response = await app.CreateBrowser().SendAsync(Requests.Get("/Sys/PipelineProbe").AsHtmx(), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("/Account/Login", new Uri(Assert.Single(response.Headers.GetValues("HX-Redirect"))).AbsolutePath);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
