using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Sys.Entities;
using Suanpan.Data;
using Suanpan.Services.Sys.Rbac.Manage.Models;
using Suanpan.Shared.Mvc;

namespace Suanpan.Services.Sys.Rbac.Manage;

public sealed partial class RoleManageService(SuanpanDbContext db, IPermissionService permissionService) : IRoleManageService
{
    public async Task<IReadOnlyList<RoleRow>> GetListAsync(CancellationToken ct = default)
    {
        var roles = await db.SysRoles.AsNoTracking()
            .OrderByDescending(r => r.IsBuiltin).ThenBy(r => r.Id)
            .ToListAsync(ct);

        var userCounts = await db.SysUserRoles
            .GroupBy(x => x.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, ct);

        var permCounts = await db.SysRolePermissions
            .GroupBy(x => x.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, ct);

        return roles.Select(r => new RoleRow(
            r.Id, r.RoleCode, r.RoleName, r.IsBuiltin,
            PresetRoleGrants.Patterns.ContainsKey(r.RoleCode),
            r.IsEnabled, r.Remark,
            userCounts.GetValueOrDefault(r.Id),
            permCounts.GetValueOrDefault(r.Id))).ToList();
    }

    public Task<SysRole?> GetAsync(long roleId, CancellationToken ct = default)
        => db.SysRoles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roleId, ct);

    public async Task<ManageResult> CreateAsync(RoleInput input, CancellationToken ct = default)
    {
        var roleCode = input.RoleCode.Trim();
        if (!RoleCodeFormat().IsMatch(roleCode))
        {
            return ManageResult.Fail("角色编码只能用小写字母、数字和下划线，以字母开头，2 ~ 64 位");
        }

        if (await db.SysRoles.AnyAsync(r => r.RoleCode == roleCode, ct))
        {
            return ManageResult.Fail($"角色编码 {roleCode} 已存在");
        }

        db.SysRoles.Add(new SysRole
        {
            RoleCode = roleCode,
            RoleName = input.RoleName.Trim(),
            IsBuiltin = false,
            IsEnabled = input.IsEnabled,
            Remark = NullIfBlank(input.Remark),
        });
        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    public async Task<ManageResult> UpdateAsync(long roleId, RoleInput input, CancellationToken ct = default)
    {
        var role = await db.SysRoles.FirstOrDefaultAsync(r => r.Id == roleId, ct);
        if (role is null)
        {
            return ManageResult.Fail("角色不存在");
        }

        if (role.IsBuiltin)
        {
            return ManageResult.Fail("内置角色不可修改");
        }

        var enabledChanged = role.IsEnabled != input.IsEnabled;
        role.RoleName = input.RoleName.Trim();
        role.IsEnabled = input.IsEnabled;
        role.Remark = NullIfBlank(input.Remark);
        await db.SaveChangesAsync(ct);

        if (enabledChanged)
        {
            await permissionService.InvalidateRoleHoldersAsync(roleId, ct);
        }

        return ManageResult.Ok;
    }

    public async Task<ManageResult> DeleteAsync(long roleId, CancellationToken ct = default)
    {
        var role = await db.SysRoles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roleId, ct);
        if (role is null)
        {
            return ManageResult.Fail("角色不存在");
        }

        if (role.IsBuiltin)
        {
            return ManageResult.Fail("内置角色不可删除");
        }

        if (await db.SysUserRoles.AnyAsync(x => x.RoleId == roleId, ct))
        {
            return ManageResult.Fail("仍有账号绑定该角色，请先解除绑定");
        }

        await db.InTransactionAsync(async () =>
        {
            await db.SysRolePermissions.Where(x => x.RoleId == roleId).ExecuteDeleteAsync(ct);
            await db.SysRoles.Where(r => r.Id == roleId).ExecuteDeleteAsync(ct);
        }, ct);

        return ManageResult.Ok;
    }

    public async Task<IReadOnlyList<long>> GetPermissionIdsAsync(long roleId, CancellationToken ct = default)
        => await db.SysRolePermissions.Where(x => x.RoleId == roleId).Select(x => x.PermId).ToListAsync(ct);

    public async Task<ManageResult> SavePermissionsAsync(long roleId, IReadOnlyCollection<long> permIds, CancellationToken ct = default)
    {
        var role = await db.SysRoles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roleId, ct);
        if (role is null)
        {
            return ManageResult.Fail("角色不存在");
        }

        if (role.IsBuiltin)
        {
            return ManageResult.Fail("内置角色不需要授权");
        }

        var ids = permIds.Distinct().ToList();
        if (ids.Count > 0 && await db.SysPermissions.CountAsync(p => ids.Contains(p.Id), ct) != ids.Count)
        {
            return ManageResult.Fail("提交的权限点包含无效项，请刷新后重试");
        }

        await db.InTransactionAsync(async () =>
        {
            await db.SysRolePermissions.Where(x => x.RoleId == roleId).ExecuteDeleteAsync(ct);
            db.SysRolePermissions.AddRange(ids.Select(id => new SysRolePermission { RoleId = roleId, PermId = id }));
            await db.SaveChangesAsync(ct);
        }, ct);

        await permissionService.InvalidateRoleHoldersAsync(roleId, ct);
        return ManageResult.Ok;
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^[a-z][a-z0-9_]{1,63}$")]
    private static partial Regex RoleCodeFormat();
}
