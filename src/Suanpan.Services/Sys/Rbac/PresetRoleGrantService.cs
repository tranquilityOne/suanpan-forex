using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Sys.Entities;
using Suanpan.Core.Sys.Rules;
using Suanpan.Data;
using Suanpan.Shared.Mvc;

namespace Suanpan.Services.Sys.Rbac;

/// <param name="grants">默认授权表；不传时用 <see cref="PresetRoleGrants.Patterns"/>（测试中可以替换）</param>
public sealed class PresetRoleGrantService(
    SuanpanDbContext db,
    IPermissionService permissionService,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? grants = null) : IPresetRoleGrantService
{
    public async Task<ManageResult> ApplyDefaultsAsync(string roleCode, CancellationToken ct = default)
    {
        if (!(grants ?? PresetRoleGrants.Patterns).TryGetValue(roleCode, out var patterns))
        {
            return ManageResult.Fail("只有预置角色可以恢复默认授权");
        }

        var role = await db.SysRoles.AsNoTracking().FirstOrDefaultAsync(r => r.RoleCode == roleCode, ct);
        if (role is null)
        {
            return ManageResult.Fail("角色不存在");
        }

        if (role.IsBuiltin)
        {
            return ManageResult.Fail("内置角色不需要授权");
        }

        var permissions = await db.SysPermissions.AsNoTracking()
            .Where(p => p.IsEnabled)
            .Select(p => new { p.Id, p.PermCode })
            .ToListAsync(ct);
        var permIds = permissions
            .Where(p => patterns.Any(pattern => PermCodePattern.IsMatch(pattern, p.PermCode)))
            .Select(p => p.Id)
            .ToList();

        await db.InTransactionAsync(async () =>
        {
            await db.SysRolePermissions.Where(x => x.RoleId == role.Id).ExecuteDeleteAsync(ct);
            db.SysRolePermissions.AddRange(permIds.Select(id => new SysRolePermission { RoleId = role.Id, PermId = id }));
            await db.SaveChangesAsync(ct);
        }, ct);

        await permissionService.InvalidateRoleHoldersAsync(role.Id, ct);
        return ManageResult.Ok;
    }
}
