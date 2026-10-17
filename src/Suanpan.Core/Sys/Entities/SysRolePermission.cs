namespace Suanpan.Core.Sys.Entities;

/// <summary>角色与权限点（sys_role_permission），(RoleId, PermId) 为联合主键。</summary>
public class SysRolePermission
{
    public long RoleId { get; set; }

    public long PermId { get; set; }
}
