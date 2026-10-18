using System.Net;
using Suanpan.Crm.Tests.Fixtures;
using Xunit;

namespace Suanpan.Crm.Tests.Auth;

/// <summary>完成标准：Hangfire 面板只有具备 sys:job:index 权限的账号能打开</summary>
public class HangfireDashboardTests
{
    [Fact]
    public async Task OnlyUsersWithJobPermissionCanOpen()
    {
        await using var app = new CrmApp();
        app.Permissions.Grant(1, "sys:job:index");
        app.Permissions.Grant(2, "sys:user:index");
        var browser = app.CreateBrowser();

        var anonymous = await browser.SendAsync(Requests.Get("/Sys/hangfire"), Ct);
        var withoutPermission = await browser.SendAsync(Requests.Get("/Sys/hangfire", app.CreateCookie(2)), Ct);
        var withPermission = await browser.SendAsync(Requests.Get("/Sys/hangfire", app.CreateCookie(1)), Ct);

        Assert.Equal("/Account/Login", anonymous.RedirectPath());
        Assert.Equal("/Account/AccessDenied", withoutPermission.RedirectPath());
        Assert.Equal(HttpStatusCode.OK, withPermission.StatusCode);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
