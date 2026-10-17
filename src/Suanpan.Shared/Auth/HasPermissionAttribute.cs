using Microsoft.AspNetCore.Authorization;

namespace Suanpan.Shared.Auth;

/// <summary>
/// 权限点标记，打在 Controller / Action 上：[HasPermission("sys:user:edit")]。
/// 本质是 Authorize 的马甲，把权限码编进 Policy 名，
/// 由 <see cref="PermissionPolicyProvider"/> 动态生成 Policy，无需启动时逐个注册。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "perm:";

    public HasPermissionAttribute(string permCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permCode);
        Policy = PolicyPrefix + permCode;
    }
}
