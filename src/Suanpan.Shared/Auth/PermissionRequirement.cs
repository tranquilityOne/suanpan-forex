using Microsoft.AspNetCore.Authorization;

namespace Suanpan.Shared.Auth;

/// <summary>动态 Policy 承载的授权要求：当前用户的权限集须包含 <see cref="PermCode"/></summary>
public sealed class PermissionRequirement(string permCode) : IAuthorizationRequirement
{
    /// <summary>权限码，三段式 {area}:{controller}:{action}</summary>
    public string PermCode { get; } = permCode;
}
