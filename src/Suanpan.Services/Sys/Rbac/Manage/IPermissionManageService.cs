using Suanpan.Core.Sys.Entities;
using Suanpan.Services.Sys.Rbac.Manage.Models;
using Suanpan.Shared.Mvc;

namespace Suanpan.Services.Sys.Rbac.Manage;

/// <summary>
/// 权限点管理（sys 设计 §6.3、§9.1）：权限点由"从代码重建"生成，这里不提供新增和删除，
/// 只允许修改图标、排序、启停。修改后菜单缓存和全部权限快照立即失效。
/// </summary>
public interface IPermissionManageService
{
    /// <summary>按树的顺序返回全部权限点（含停用的），带层级深度</summary>
    Task<IReadOnlyList<PermissionRow>> GetTreeAsync(CancellationToken ct = default);

    Task<SysPermission?> GetAsync(long permId, CancellationToken ct = default);

    Task<ManageResult> UpdateAsync(PermissionEditInput input, CancellationToken ct = default);
}
