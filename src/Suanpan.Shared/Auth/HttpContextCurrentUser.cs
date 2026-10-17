using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Suanpan.Shared.Auth;

/// <summary>
/// 从 HttpContext 的登录 Claims 取当前用户。
/// 没有 HttpContext（后台任务、启动期）或未登录时各属性为空。
/// </summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal
    {
        get
        {
            var user = accessor.HttpContext?.User;
            return user?.Identity?.IsAuthenticated == true ? user : null;
        }
    }

    public long? UserId
        => long.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? UserName => Principal?.Identity?.Name;
}
