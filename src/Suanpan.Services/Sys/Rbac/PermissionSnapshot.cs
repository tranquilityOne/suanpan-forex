namespace Suanpan.Services.Sys.Rbac;

/// <summary>
/// 账号的权限快照：是否管理员、持有的（启用的）角色编码、权限码集合。
/// 管理员不挂权限记录，判断时直接放行。
/// </summary>
public sealed record PermissionSnapshot(bool IsSuperAdmin, IReadOnlyList<string> RoleCodes, IReadOnlySet<string> Codes)
{
    public static readonly PermissionSnapshot Empty = new(false, [], new HashSet<string>(StringComparer.Ordinal));

    public bool Has(string permCode) => IsSuperAdmin || Codes.Contains(permCode);
}
