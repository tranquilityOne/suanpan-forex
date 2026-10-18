using Suanpan.Services.Sys.Rbac.Models;

namespace Suanpan.Services.Sys.Rbac;

/// <summary>
/// 侧边栏菜单（sys 设计 §6.4）：当前账号有权限的"目录 + 菜单"树。
/// 管理员看到全部启用的菜单；目录在其下至少有一个可见菜单时才显示。菜单源数据缓存 5 分钟。
/// </summary>
public interface IMenuService
{
    Task<IReadOnlyList<MenuNode>> GetUserMenusAsync(long userId, CancellationToken ct = default);
}
