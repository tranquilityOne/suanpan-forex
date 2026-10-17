using Suanpan.Core.Common.Auditing;

namespace Suanpan.Core.Sys.Entities;

/// <summary>角色（sys_role）。</summary>
public class SysRole : AuditedEntity
{
    public long Id { get; set; }

    /// <summary>角色编码，唯一，如 super_admin、engineer</summary>
    public string RoleCode { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    /// <summary>内置角色不可修改、删除和授权；super_admin 在授权时直接放行，不挂权限记录</summary>
    public bool IsBuiltin { get; set; }

    public bool IsEnabled { get; set; } = true;

    public string? Remark { get; set; }
}
