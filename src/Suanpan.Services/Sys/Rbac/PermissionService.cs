using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Suanpan.Core.Sys.Constants;
using Suanpan.Core.Sys.Enums;
using Suanpan.Data;

namespace Suanpan.Services.Sys.Rbac;

public sealed class PermissionService(SuanpanDbContext db, IMemoryCache cache) : IPermissionService
{
    private const string VersionKey = "perms:version";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<PermissionSnapshot> GetSnapshotAsync(long userId, CancellationToken ct = default)
    {
        var snapshot = await cache.GetOrCreateAsync(Key(userId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await LoadAsync(userId, ct);
        });

        return snapshot ?? PermissionSnapshot.Empty;
    }

    public void Invalidate(long userId) => cache.Remove(Key(userId));

    public async Task InvalidateRoleHoldersAsync(long roleId, CancellationToken ct = default)
    {
        var userIds = await db.SysUserRoles.Where(x => x.RoleId == roleId).Select(x => x.UserId).ToListAsync(ct);
        foreach (var userId in userIds)
        {
            Invalidate(userId);
        }
    }

    public void InvalidateAll() => cache.Set(VersionKey, CurrentVersion() + 1);

    /// <summary>缓存键：perms:{版本号}:{账号 id}（sys 设计 §6.5）</summary>
    private string Key(long userId) => $"perms:{CurrentVersion()}:{userId}";

    private long CurrentVersion() => cache.TryGetValue(VersionKey, out long version) ? version : 0;

    private async Task<PermissionSnapshot> LoadAsync(long userId, CancellationToken ct)
    {
        // 待激活、已停用的账号没有任何业务权限
        if (!await db.SysUsers.AnyAsync(u => u.Id == userId && u.Status == UserStatus.Active, ct))
        {
            return PermissionSnapshot.Empty;
        }

        var roles = await (
            from ur in db.SysUserRoles
            join r in db.SysRoles on ur.RoleId equals r.Id
            where ur.UserId == userId && r.IsEnabled
            select new { r.Id, r.RoleCode }
        ).ToListAsync(ct);

        if (roles.Count == 0)
        {
            return PermissionSnapshot.Empty;
        }

        var roleCodes = roles.Select(r => r.RoleCode).Order(StringComparer.Ordinal).ToList();
        if (roleCodes.Contains(RoleCodes.SuperAdmin))
        {
            return new PermissionSnapshot(true, roleCodes, new HashSet<string>(StringComparer.Ordinal));
        }

        var roleIds = roles.Select(r => r.Id).ToList();
        var codes = await (
            from rp in db.SysRolePermissions
            join p in db.SysPermissions on rp.PermId equals p.Id
            where roleIds.Contains(rp.RoleId) && p.IsEnabled
            select p.PermCode
        ).Distinct().ToListAsync(ct);

        return new PermissionSnapshot(false, roleCodes, codes.ToHashSet(StringComparer.Ordinal));
    }
}
