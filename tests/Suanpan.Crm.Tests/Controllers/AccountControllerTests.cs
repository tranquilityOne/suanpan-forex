using System.Net;
using Suanpan.Core.Sys.Enums;
using Suanpan.Crm.Tests.Fixtures;
using Xunit;
using static Suanpan.Crm.Tests.Fixtures.BrowserForms;

namespace Suanpan.Crm.Tests.Controllers;

/// <summary>
/// 登录流程中 Cookie 的签发、切换和跳转（sys 设计 §5.1、§5.2）。浏览器自动保存 Cookie，表单带防伪令牌提交。
/// 登录服务用替身，它本身的校验在 Suanpan.Services.Tests 中测试。
/// </summary>
public class AccountControllerTests
{
    /// <summary>完成标准（JAN-68）：管理员能完成"密码 → TOTP → 进入首页"；只通过密码时还进不了首页</summary>
    [Fact]
    public async Task PasswordThenTotp_ReachesHome()
    {
        await using var app = new CrmApp();
        app.Auth.AddAccount(7, SignInStep.VerifyTotp);
        var browser = app.CreateBrowser();

        var login = await SubmitAsync(browser, "/Account/Login", new() { ["Username"] = "user7", ["Password"] = FakeAuthService.Password });
        var homeBeforeTotp = await browser.GetAsync("/", Ct);
        var verify = await SubmitAsync(browser, "/Account/TwoFactor", new() { ["Code"] = FakeAuthService.Code });

        Assert.Equal("/Account/TwoFactor", login.RedirectPath());
        Assert.Equal("/Account/Login", homeBeforeTotp.RedirectPath());
        Assert.Equal("/", verify.RedirectPath());
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/", Ct)).StatusCode);
    }

    [Fact]
    public async Task WrongPassword_IssuesNoCookie()
    {
        await using var app = new CrmApp();
        app.Auth.AddAccount(7, SignInStep.VerifyTotp);
        var browser = app.CreateBrowser();

        var login = await SubmitAsync(browser, "/Account/Login", new() { ["Username"] = "user7", ["Password"] = "Wrong-pass-2026" });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal("/Account/Login", (await browser.GetAsync("/Account/TwoFactor", Ct)).RedirectPath());
    }

    /// <summary>完成标准（JAN-71）：新账号必须先改密码、再绑定 TOTP，不能跳步，完成后进入首页</summary>
    [Fact]
    public async Task NewAccount_IsGuidedThroughEachStep()
    {
        await using var app = new CrmApp();
        app.Auth.AddAccount(8, SignInStep.ChangePassword);
        var browser = app.CreateBrowser();

        var login = await SubmitAsync(browser, "/Account/Login", new() { ["Username"] = "user8", ["Password"] = FakeAuthService.Password });
        var skipToTotp = await browser.GetAsync("/Account/TwoFactor", Ct);
        var changed = await SubmitAsync(browser, "/Account/ChangePassword",
            new() { ["NewPassword"] = "Brand-new-pass-2026", ["ConfirmPassword"] = "Brand-new-pass-2026" });
        var bound = await SubmitAsync(browser, "/Account/TwoFactorSetup",
            new() { ["Code"] = FakeAuthService.Code, ["ProtectedSecret"] = "protected-secret" });

        Assert.Equal("/Account/ChangePassword", login.RedirectPath());
        Assert.Equal("/Account/ChangePassword", skipToTotp.RedirectPath());
        Assert.Equal("/Account/TwoFactorSetup", changed.RedirectPath());
        Assert.Equal("/", bound.RedirectPath());
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/", Ct)).StatusCode);
    }

    /// <summary>完成标准（JAN-68）：修改密码、重新绑定 TOTP 后，其他设备上的会话失效，当前会话不受影响</summary>
    [Theory]
    [InlineData("/Account/ChangePassword")]
    [InlineData("/Account/TwoFactorSetup")]
    public async Task AccountChange_KeepsThisSession_EndsOtherSessions(string page)
    {
        await using var app = new CrmApp();
        app.Auth.AddAccount(9, SignInStep.VerifyTotp);
        var thisDevice = await SignedInBrowserAsync(app, "user9");
        var otherDevice = await SignedInBrowserAsync(app, "user9");

        var change = await SubmitAsync(thisDevice, page, page == "/Account/ChangePassword"
            ? new() { ["CurrentPassword"] = FakeAuthService.Password, ["NewPassword"] = "Brand-new-pass-2026", ["ConfirmPassword"] = "Brand-new-pass-2026" }
            : new() { ["Code"] = FakeAuthService.Code, ["ProtectedSecret"] = "protected-secret" });

        Assert.Equal("/", change.RedirectPath());
        Assert.Equal(HttpStatusCode.OK, (await thisDevice.GetAsync("/", Ct)).StatusCode);
        Assert.Equal("/Account/Login", (await otherDevice.GetAsync("/", Ct)).RedirectPath());
    }

    [Fact]
    public async Task Logout_EndsSessionAndIsLogged()
    {
        await using var app = new CrmApp();
        app.Auth.AddAccount(10, SignInStep.VerifyTotp);
        var browser = await SignedInBrowserAsync(app, "user10");

        var logout = await SubmitAsync(browser, "/", new(), action: "/Account/Logout");

        Assert.Equal("/Account/Login", logout.RedirectPath());
        Assert.Equal("/Account/Login", (await browser.GetAsync("/", Ct)).RedirectPath());
        Assert.Contains("user10", app.Auth.LoggedOut);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
