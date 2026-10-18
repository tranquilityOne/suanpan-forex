using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Suanpan.Core.Sys.Entities;
using Suanpan.Core.Sys.Enums;
using Suanpan.Data;
using Suanpan.Services.Sys.Rbac.Models;
using Suanpan.Shared.Auth;

namespace Suanpan.Services.Sys.Rbac;

public sealed class PermissionRebuildService(
    IActionDescriptorCollectionProvider actionProvider,
    SuanpanDbContext db,
    IPermissionService permissionService,
    IMemoryCache cache,
    ILogger<PermissionRebuildService> logger) : IPermissionRebuildService
{
    private sealed record ActionNode(string Code, string Name);

    private sealed record MenuGroup(
        string Area, string MenuCode, string MenuName, string? Icon, int Sort, string Url, IReadOnlyList<ActionNode> Buttons);

    public async Task<RebuildReport> RebuildAsync(IReadOnlyList<AreaDirectory> areas, CancellationToken ct = default)
    {
        var (groups, missingMenu) = ScanEndpoints();

        var existing = await db.SysPermissions.ToListAsync(ct);
        var byCode = existing.ToDictionary(p => p.PermCode, StringComparer.Ordinal);
        var desired = new HashSet<string>(StringComparer.Ordinal);
        var disabledCodes = new List<string>();
        var reEnabledCodes = new List<string>();
        int added = 0, updated = 0;

        SysPermission Upsert(string code, string name, PermType type, long parentId, string? url, string? icon, int sort)
        {
            desired.Add(code);
            if (byCode.TryGetValue(code, out var row))
            {
                var changed = row.PermName != name || row.PermType != type || row.ParentId != parentId
                              || row.MenuUrl != url || row.Icon != icon || row.Sort != sort || !row.IsEnabled;
                if (!row.IsEnabled)
                {
                    reEnabledCodes.Add(code);
                }

                if (changed)
                {
                    row.PermName = name;
                    row.PermType = type;
                    row.ParentId = parentId;
                    row.MenuUrl = url;
                    row.Icon = icon;
                    row.Sort = sort;
                    row.IsEnabled = true;
                    updated++;
                }

                return row;
            }

            var created = new SysPermission
            {
                PermCode = code,
                PermName = name,
                PermType = type,
                ParentId = parentId,
                MenuUrl = url,
                Icon = icon,
                Sort = sort,
                IsEnabled = true,
            };
            db.SysPermissions.Add(created);
            byCode[code] = created;
            added++;
            return created;
        }

        await db.InTransactionAsync(async () =>
        {
            // 逐层保存：目录、菜单要先拿到 id 才能挂子节点
            foreach (var area in areas)
            {
                var areaGroups = groups
                    .Where(g => g.Area.Equals(area.Area, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(g => g.Sort)
                    .ToList();
                if (areaGroups.Count == 0)
                {
                    continue; // 没有页面的栏目不建目录
                }

                var directory = Upsert(PermCodeHelper.ToSnake(area.Area), area.Name, PermType.Directory, 0, null, area.Icon, area.Sort);
                await db.SaveChangesAsync(ct);

                foreach (var group in areaGroups)
                {
                    var menu = Upsert(group.MenuCode, group.MenuName, PermType.Menu, directory.Id, group.Url, group.Icon, group.Sort);
                    await db.SaveChangesAsync(ct);

                    var buttonSort = 1;
                    foreach (var button in group.Buttons)
                    {
                        Upsert(button.Code, button.Name, PermType.Button, menu.Id, null, null, buttonSort++);
                    }
                }
            }

            // 代码中已不存在的权限码：停用，不删除
            foreach (var row in existing.Where(p => p.IsEnabled && !desired.Contains(p.PermCode)))
            {
                row.IsEnabled = false;
                disabledCodes.Add(row.PermCode);
            }

            await db.SaveChangesAsync(ct);
        }, ct);

        cache.Remove(MenuService.SourceCacheKey);
        permissionService.InvalidateAll();

        logger.LogInformation("权限树重建完成：新增 {Added}，更新 {Updated}，停用 {Disabled}", added, updated, disabledCodes.Count);
        return new RebuildReport(added, updated, disabledCodes.Count, disabledCodes, missingMenu, reEnabledCodes);
    }

    /// <summary>扫描 Area 内的控制器 action，按控制器聚合成菜单组；同一个权限码只算一次（GET、POST 同名 action）</summary>
    private (List<MenuGroup> Groups, List<string> MissingMenu) ScanEndpoints()
    {
        var groups = new List<MenuGroup>();
        var missingMenu = new List<string>();

        var byController = actionProvider.ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(d => d.RouteValues.TryGetValue("area", out var area) && !string.IsNullOrEmpty(area))
            .GroupBy(d => (Area: d.RouteValues["area"]!, d.ControllerName));

        foreach (var controller in byController)
        {
            var (area, controllerName) = controller.Key;

            var actions = new Dictionary<string, ActionNode>(StringComparer.Ordinal);
            string? indexCode = null;
            foreach (var descriptor in controller)
            {
                var permission = descriptor.EndpointMetadata.OfType<HasPermissionAttribute>().FirstOrDefault();
                if (permission?.Policy is null)
                {
                    continue; // 允许匿名或不受权限管理的 action
                }

                var code = permission.Policy[HasPermissionAttribute.PolicyPrefix.Length..];
                if (descriptor.ActionName == "Index")
                {
                    indexCode = code;
                }

                if (!actions.ContainsKey(code))
                {
                    var name = descriptor.MethodInfo.GetCustomAttribute<PermNameAttribute>()?.Name
                               ?? PermCodeHelper.DefaultActionName(descriptor.ActionName);
                    actions[code] = new ActionNode(code, name);
                }
            }

            if (actions.Count == 0)
            {
                continue;
            }

            var menuAttribute = controller.First().ControllerTypeInfo.GetCustomAttribute<PermMenuAttribute>();
            if (menuAttribute is null)
            {
                missingMenu.Add($"{area}/{controllerName}");
            }

            // 菜单的权限码取 Index 的码（能看列表就能进菜单），Index 不再单独生成按钮
            var menuCode = indexCode ?? actions.Keys.First();
            groups.Add(new MenuGroup(
                area,
                menuCode,
                menuAttribute?.Name ?? controllerName,
                menuAttribute?.Icon,
                menuAttribute?.Sort ?? 0,
                $"/{area}/{controllerName}",
                actions.Values.Where(a => a.Code != menuCode).ToList()));
        }

        return (groups, missingMenu);
    }
}
