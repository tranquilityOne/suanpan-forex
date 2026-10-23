using System.Text.RegularExpressions;
using Xunit;

namespace Suanpan.Crm.Tests.Fixtures;

/// <summary>模拟浏览器提交表单：先打开页面取防伪令牌再提交；用替身登录服务完成登录</summary>
public static partial class BrowserForms
{
    /// <summary>打开表单所在的页面取防伪令牌，再提交表单（默认提交到同一地址）</summary>
    public static async Task<HttpResponseMessage> SubmitAsync(
        HttpClient browser, string page, Dictionary<string, string> fields, string? action = null)
    {
        var html = await browser.GetStringAsync(page, Ct);
        fields["__RequestVerificationToken"] = AntiforgeryToken().Match(html).Groups["token"].Value;
        return await browser.PostAsync(action ?? page, new FormUrlEncodedContent(fields), Ct);
    }

    /// <summary>完成"密码 → TOTP"后进入首页的浏览器；账号要先用 app.Auth.AddAccount 登记</summary>
    public static async Task<HttpClient> SignedInBrowserAsync(CrmApp app, string username)
    {
        var browser = app.CreateBrowser();
        await SubmitAsync(browser, "/Account/Login", new() { ["Username"] = username, ["Password"] = FakeAuthService.Password });
        var verify = await SubmitAsync(browser, "/Account/TwoFactor", new() { ["Code"] = FakeAuthService.Code });
        Assert.Equal("/", verify.RedirectPath());
        return browser;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"(?<token>[^\"]+)\"")]
    private static partial Regex AntiforgeryToken();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
