using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Suanpan.Core.Sys.Entities;
using Suanpan.Core.Sys.Enums;
using Suanpan.Data;
using Suanpan.Services.Sys.Rbac.Models;

namespace Suanpan.Services.Sys.Rbac;

public sealed class MenuService(SuanpanDbContext db, IMemoryCache cache, IPermissionService permissionService) : IMenuService
{
    /// <summary>菜单源数据的缓存键（sys 设计 §6.5）；权限点修改、权限树重建后失效</summary>
    public const string SourceCacheKey = "menusrc";

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<IReadOnlyList<MenuNode>> GetUserMenusAsync(long userId, CancellationToken ct = default)
    {
        var all = await GetSourceAsync(ct);
        if (all.Count == 0)
        {
            return [];
        }

        var snapshot = await permissionService.GetSnapshotAsync(userId, ct);

        var visible = all.Where(p => p.PermType == PermType.Menu && snapshot.Has(p.PermCode))
            .Select(p => p.Id)
            .ToHashSet();

        // 沿父链把目录点亮
        var byId = all.ToDictionary(p => p.Id);
        foreach (var id in visible.ToList())
        {
            var current = byId[id];
            while (current.ParentId != 0 && byId.TryGetValue(current.ParentId, out var parent) && visible.Add(parent.Id))
            {
                current = parent;
            }
        }

        return BuildTree(all, visible, parentId: 0);
    }

    private static List<MenuNode> BuildTree(IReadOnlyList<SysPermission> all, HashSet<long> visible, long parentId)
        => all.Where(p => p.ParentId == parentId && visible.Contains(p.Id))
            .OrderBy(p => p.Sort).ThenBy(p => p.Id)
            .Select(p => new MenuNode(p.Id, p.PermName, p.MenuUrl, p.Icon, BuildTree(all, visible, p.Id)))
            .ToList();

    private async Task<IReadOnlyList<SysPermission>> GetSourceAsync(CancellationToken ct)
    {
        var source = await cache.GetOrCreateAsync(SourceCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await db.SysPermissions.AsNoTracking()
                .Where(p => p.IsEnabled && p.PermType != PermType.Button)
                .ToListAsync(ct);
        });

        return source ?? [];
    }
}
