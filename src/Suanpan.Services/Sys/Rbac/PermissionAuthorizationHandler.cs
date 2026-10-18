using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Suanpan.Shared.Auth;

namespace Suanpan.Services.Sys.Rbac;

/// <summary>
/// 动态 Policy 三件套中的授权处理器（sys 设计 §6.1）：从登录信息取账号 id，查权限快照，
/// 管理员直接放行，其他人检查是否持有要求的权限码。
/// </summary>
public sealed class PermissionAuthorizationHandler(IPermissionService permissionService)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || !long.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return;
        }

        var snapshot = await permissionService.GetSnapshotAsync(userId);
        if (snapshot.Has(requirement.PermCode))
        {
            context.Succeed(requirement);
        }
    }
}
