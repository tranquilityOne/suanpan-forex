using System.Net;
using Suanpan.Crm.Tests.Fixtures;
using Xunit;

namespace Suanpan.Crm.Tests.Auth;

/// <summary>
/// 完成标准：打开 IP 白名单后，非白名单 IP 返回 403，应急放行 IP 不受影响（sys 设计 §5.5、FR-028 #2）。
/// 请求默认经本机的反向代理转发（来源 127.0.0.1，客户端 IP 在 X-Forwarded-For 中）。
/// </summary>
public class IpWhitelistMiddlewareTests
{
    private const string WhitelistedIp = "203.0.113.10";

    [Theory]
    [InlineData(false, "127.0.0.1", "203.0.113.5", true)]       // 总开关关闭：不检查
    [InlineData(true, "127.0.0.1", "203.0.113.5", false)]       // 不在白名单
    [InlineData(true, "127.0.0.1", WhitelistedIp, true)]        // 在白名单（经可信代理转发）
    [InlineData(true, CrmApp.BootstrapIp, null, true)]          // 应急放行 IP
    [InlineData(true, "192.0.2.1", WhitelistedIp, false)]       // 不可信的来源伪造 X-Forwarded-For
    public async Task OnlyAllowedSourcesPass(bool enabled, string remoteIp, string? forwardedFor, bool allowed)
    {
        await using var app = new CrmApp();
        app.Config.IpWhitelistEnabled = enabled;
        app.Whitelist.Allowed.Add(IPAddress.Parse(WhitelistedIp));

        var response = await app.CreateBrowser().SendAsync(Requests.Get("/RootProbe").From(remoteIp, forwardedFor), Ct);

        // 放行后未登录，跳转登录页；被拦截时返回 403
        Assert.Equal(allowed, response.StatusCode != HttpStatusCode.Forbidden);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
