using Suanpan.Services.Sys.Rbac.Manage.Models;

namespace Suanpan.Crm.Models.Sys;

/// <summary>角色授权：启用的权限点按树的顺序排列，勾选已授权的</summary>
public sealed class RoleAssignViewModel
{
    public required long RoleId { get; init; }

    public required string RoleName { get; init; }

    public required IReadOnlyList<PermissionRow> Permissions { get; init; }

    public required IReadOnlySet<long> GrantedIds { get; init; }
}
