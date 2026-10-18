using Suanpan.Services.Sys.Rbac.Models;

namespace Suanpan.Services.Sys.Rbac;

/// <summary>
/// 权限树重建（sys 设计 §6.3）：从路由端点推出"目录（Area）→ 菜单（Controller）→ 按钮（Action）"三层树，与 sys_permission 对齐。
/// 代码是唯一的事实来源：名称、层级、路由、图标、排序以代码为准覆盖；代码中已不存在的权限码停用、不删除；
/// 已存在的行保留 id，角色授权不会丢。权限码直接读端点元数据上的 [HasPermission]，与运行时校验的是同一份。
/// 依赖 MVC 的 IActionDescriptorCollectionProvider，只由 Crm 注册。
/// </summary>
public interface IPermissionRebuildService
{
    Task<RebuildReport> RebuildAsync(IReadOnlyList<AreaDirectory> areas, CancellationToken ct = default);
}
