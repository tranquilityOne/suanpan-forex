using System.Net;
using Suanpan.Crm.Tests.Fixtures;
using Xunit;

namespace Suanpan.Crm.Tests.Auth;

/// <summary>Area 内的页面默认拒绝，权限码按 {area}:{controller}:{action} 自动生成（sys 设计 §6.2、FR-028 #6）</summary>
public class AutoPermissionConventionTests
{
    private const long UserId = 7;

    [Fact]
    public async Task WithoutPermission_IsDenied()
    {
        await using var app = new CrmApp();
        app.Permissions.Grant(UserId, "sys:other:index");
        var browser = app.CreateBrowser();

        var page = await browser.SendAsync(Requests.Get("/Sys/PipelineProbe", app.CreateCookie(UserId)), Ct);
        var htmx = await browser.SendAsync(Requests.Get("/Sys/PipelineProbe", app.CreateCookie(UserId)).AsHtmx(), Ct);

        Assert.Equal("/Account/AccessDenied", page.RedirectPath());
        Assert.Equal(HttpStatusCode.Forbidden, htmx.StatusCode);
    }

    [Fact]
    public async Task WithDerivedPermission_IsAllowed()
    {
        await using var app = new CrmApp();
        app.Permissions.Grant(UserId, "sys:pipeline_probe:index");

        var response = await app.CreateBrowser().SendAsync(Requests.Get("/Sys/PipelineProbe", app.CreateCookie(UserId)), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>显式标了 [HasPermission] 的 action 只认显式的码，不再自动生成</summary>
    [Fact]
    public async Task ExplicitPermission_ReplacesDerivedCode()
    {
        await using var app = new CrmApp();
        app.Permissions.Grant(1, "sys:pipeline_probe:shared");
        app.Permissions.Grant(2, "sys:pipeline_probe:export");
        var browser = app.CreateBrowser();

        var withExplicit = await browser.SendAsync(Requests.Get("/Sys/PipelineProbe/Export", app.CreateCookie(1)), Ct);
        var withDerived = await browser.SendAsync(Requests.Get("/Sys/PipelineProbe/Export", app.CreateCookie(2)), Ct);

        Assert.Equal(HttpStatusCode.OK, withExplicit.StatusCode);
        Assert.Equal("/Account/AccessDenied", withDerived.RedirectPath());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
