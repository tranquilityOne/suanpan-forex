using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Suanpan.Core.Sys.Enums;
using Suanpan.Crm.Auth;
using Suanpan.Crm.Tests.Fixtures;
using Xunit;
using static Suanpan.Crm.Tests.Fixtures.BrowserForms;

namespace Suanpan.Crm.Tests.Controllers;

/// <summary>
/// 开发环境跳过 TOTP 验证码（sys 设计 §5.1）：只有 Development 环境开启 Auth:SkipTotpInDevelopment 时，已绑定账号输完密码直接进入首页；
/// 其他环境配了也要输入验证码（BR-140）。登录服务用替身，跳过时写库和写日志在 Suanpan.Services.Tests 中测试。
/// </summary>
public class TotpSkipTests
{
    [Theory]
    [InlineData("Development", "/")]
    [InlineData("Testing", "/Account/TwoFactor")]
    public async Task Login_SkipsTotpOnlyInDevelopment(string environment, string expected)
    {
        await using var app = new CrmApp(environment);
        app.Auth.AddAccount(12, SignInStep.VerifyTotp);
        var browser = BrowserWithSkipSwitch(app);

        var login = await SubmitAsync(browser, "/Account/Login", new() { ["Username"] = "user12", ["Password"] = FakeAuthService.Password });

        Assert.Equal(expected, login.RedirectPath());
    }

    /// <summary>只跳过验证码：必须改密码、尚未绑定 TOTP 的账号照常走这两步</summary>
    [Fact]
    public async Task Login_StillRequiresPasswordChangeAndTotpSetup()
    {
        await using var app = new CrmApp("Development");
        app.Auth.AddAccount(13, SignInStep.ChangePassword);
        var browser = BrowserWithSkipSwitch(app);

        var login = await SubmitAsync(browser, "/Account/Login", new() { ["Username"] = "user13", ["Password"] = FakeAuthService.Password });
        var changed = await SubmitAsync(browser, "/Account/ChangePassword",
            new() { ["NewPassword"] = "Brand-new-pass-2026", ["ConfirmPassword"] = "Brand-new-pass-2026" });

        Assert.Equal("/Account/ChangePassword", login.RedirectPath());
        Assert.Equal("/Account/TwoFactorSetup", changed.RedirectPath());
    }

    /// <summary>与 CrmApp.CreateBrowser 相同的浏览器，另外打开跳过开关</summary>
    private static HttpClient BrowserWithSkipSwitch(CrmApp app)
        => app.WithWebHostBuilder(builder => builder.UseSetting(TotpSkipSwitch.ConfigKey, "true"))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
}
