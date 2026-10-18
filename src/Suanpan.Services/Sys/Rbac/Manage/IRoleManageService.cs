using Suanpan.Core.Sys.Entities;
using Suanpan.Services.Sys.Rbac.Manage.Models;
using Suanpan.Shared.Mvc;

namespace Suanpan.Services.Sys.Rbac.Manage;

/// <summary>
/// 角色管理（sys 设计 §9.1）。内置角色（管理员）不可修改、删除和授权；
/// 授权变更、启停后，持有该角色的账号的权限快照立即失效。"恢复默认授权"见 <see cref="IPresetRoleGrantService"/>。
/// </summary>
public interface IRoleManageService
{
    Task<IReadOnlyList<RoleRow>> GetListAsync(CancellationToken ct = default);

    Task<SysRole?> GetAsync(long roleId, CancellationToken ct = default);

    /// <summary>角色编码为小写字母开头的 snake_case，创建后不可修改</summary>
    Task<ManageResult> CreateAsync(RoleInput input, CancellationToken ct = default);

    /// <summary>修改名称、启停、备注；忽略输入中的角色编码</summary>
    Task<ManageResult> UpdateAsync(long roleId, RoleInput input, CancellationToken ct = default);

    /// <summary>删除角色；仍有账号绑定时拒绝</summary>
    Task<ManageResult> DeleteAsync(long roleId, CancellationToken ct = default);

    Task<IReadOnlyList<long>> GetPermissionIdsAsync(long roleId, CancellationToken ct = default);

    /// <summary>整体覆盖角色的授权</summary>
    Task<ManageResult> SavePermissionsAsync(long roleId, IReadOnlyCollection<long> permIds, CancellationToken ct = default);
}
