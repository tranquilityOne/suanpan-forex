using System.Net;
using Suanpan.Services.Sys.Configs;
using Suanpan.Services.Sys.Security;

namespace Suanpan.Crm.Auth;

/// <summary>
/// 后台访问 IP 白名单（sys 设计 §5.5、BR-141）：在认证之前拦截所有请求，包括登录页和静态资源。
/// 总开关为配置 security:admin_ip_whitelist_enabled，默认关闭；应急放行 IP（Security:BootstrapAllowedIps）总是放行，
/// 防止把所有人都挡在外面。客户端 IP 取自 ForwardedHeaders 处理后的地址。
/// 被拒绝时返回 403，不说明原因（ERR-AUTH-003）。
/// </summary>
public sealed class IpWhitelistMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<IpWhitelistMiddleware> logger)
{
    private const string DeniedPage = "<!doctype html><meta charset=\"utf-8\"><title>403</title><p>禁止访问</p>";

    private readonly IReadOnlyList<string> _bootstrapAllowed = IpListConfig.Parse(configuration, "Security:BootstrapAllowedIps");

    public async Task InvokeAsync(HttpContext context, IConfigService configService, IIpWhitelistService whitelist)
    {
        var ct = context.RequestAborted;
        var enabled = await configService.GetBoolAsync(
            SysConfigKeys.AdminIpWhitelistEnabled, SysConfigKeys.AdminIpWhitelistEnabledDefault, ct);

        if (!enabled || await IsAllowedAsync(context.Connection.RemoteIpAddress, whitelist, ct))
        {
            await next(context);
            return;
        }

        logger.LogWarning("后台访问被 IP 白名单拒绝：{Ip} {Path}", context.Connection.RemoteIpAddress, context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(DeniedPage, ct);
    }

    private async Task<bool> IsAllowedAsync(IPAddress? remoteIp, IIpWhitelistService whitelist, CancellationToken ct)
    {
        if (remoteIp is null)
        {
            return false;
        }

        var ip = IpWhitelistMatcher.Normalize(remoteIp);
        return _bootstrapAllowed.Any(entry => IpWhitelistMatcher.Matches(entry, ip))
               || await whitelist.IsAllowedAsync(ip, ct);
    }
}
