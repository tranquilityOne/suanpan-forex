using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Suanpan.Crm.Mvc;
using Suanpan.Services.Sys.Auth;
using Suanpan.Shared.Auth;

namespace Suanpan.Crm.Auth;

/// <summary>正式 Cookie 和待完成 Cookie 共用的事件处理</summary>
internal static class CrmCookieEvents
{
    public static CookieAuthenticationEvents Create() => new()
    {
        OnValidatePrincipal = ValidateSecurityStampAsync,
        OnRedirectToLogin = context => RedirectAsync(context, StatusCodes.Status401Unauthorized),
        OnRedirectToAccessDenied = context => RedirectAsync(context, StatusCodes.Status403Forbidden),
    };

    /// <summary>
    /// 逐请求比对 Cookie 中的安全戳（sys 设计 §5.3）：与库中不一致、账号已停用或不存在时注销，
    /// 改密码、重置、停用后旧 Cookie 随即失效。比对结果缓存 60 秒，见 <see cref="ISecurityStampValidator"/>。
    /// </summary>
    private static async Task ValidateSecurityStampAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var validator = context.HttpContext.RequestServices.GetRequiredService<ISecurityStampValidator>();
        if (long.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            && await validator.ValidateAsync(userId, principal.FindFirstValue(SuanpanClaimTypes.SecurityStamp), context.HttpContext.RequestAborted))
        {
            return;
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(context.Scheme.Name);
    }

    /// <summary>
    /// HTMX 请求不能跟随 302（会把登录页塞进局部区域）：未登录返回 401，并用 HX-Redirect 让整页跳转到登录页；
    /// 无权限返回 403，由 site.js 统一提示（ERR-AUTH-002）。普通请求照常跳转。
    /// </summary>
    private static Task RedirectAsync(RedirectContext<CookieAuthenticationOptions> context, int htmxStatusCode)
    {
        if (!context.Request.IsHtmx())
        {
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        }

        context.Response.StatusCode = htmxStatusCode;
        if (htmxStatusCode == StatusCodes.Status401Unauthorized)
        {
            context.Response.Headers["HX-Redirect"] = context.RedirectUri;
        }

        return Task.CompletedTask;
    }
}
