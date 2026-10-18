using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Suanpan.Core.Sys.Entities;
using Suanpan.Data;
using Suanpan.Services.Sys.Rbac.Manage.Models;
using Suanpan.Shared.Mvc;

namespace Suanpan.Services.Sys.Rbac.Manage;

public sealed class PermissionManageService(SuanpanDbContext db, IMemoryCache cache, IPermissionService permissionService)
    : IPermissionManageService
{
    public async Task<IReadOnlyList<PermissionRow>> GetTreeAsync(CancellationToken ct = default)
    {
        var all = await db.SysPermissions.AsNoTracking()
            .OrderBy(p => p.Sort).ThenBy(p => p.Id)
            .ToListAsync(ct);

        var rows = new List<PermissionRow>(all.Count);
        void Walk(long parentId, int depth)
        {
            foreach (var p in all.Where(x => x.ParentId == parentId))
            {
                rows.Add(ToRow(p, depth));
                Walk(p.Id, depth + 1);
            }
        }

        Walk(0, 0);

        // 父节点丢失的孤儿也列出来，避免看不到
        var seen = rows.Select(r => r.Id).ToHashSet();
        rows.AddRange(all.Where(p => !seen.Contains(p.Id)).Select(p => ToRow(p, 0)));
        return rows;
    }

    public Task<SysPermission?> GetAsync(long permId, CancellationToken ct = default)
        => db.SysPermissions.AsNoTracking().FirstOrDefaultAsync(p => p.Id == permId, ct);

    public async Task<ManageResult> UpdateAsync(PermissionEditInput input, CancellationToken ct = default)
    {
        var permission = await db.SysPermissions.FirstOrDefaultAsync(p => p.Id == input.Id, ct);
        if (permission is null)
        {
            return ManageResult.Fail("权限点不存在");
        }

        permission.Icon = string.IsNullOrWhiteSpace(input.Icon) ? null : input.Icon.Trim();
        permission.Sort = input.Sort;
        permission.IsEnabled = input.IsEnabled;
        await db.SaveChangesAsync(ct);

        cache.Remove(MenuService.SourceCacheKey);
        permissionService.InvalidateAll();
        return ManageResult.Ok;
    }

    private static PermissionRow ToRow(SysPermission p, int depth)
        => new(p.Id, p.ParentId, p.PermType, p.PermCode, p.PermName, p.MenuUrl, p.Icon, p.Sort, p.IsEnabled, depth);
}
