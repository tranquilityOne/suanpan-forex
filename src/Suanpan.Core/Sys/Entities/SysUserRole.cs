using Suanpan.Core.Common.Auditing;

namespace Suanpan.Core.Sys.Entities;

/// <summary>账号与角色（sys_user_role），(UserId, RoleId) 为联合主键。</summary>
public class SysUserRole : CreationAuditedEntity
{
    public long UserId { get; set; }

    public long RoleId { get; set; }
}
