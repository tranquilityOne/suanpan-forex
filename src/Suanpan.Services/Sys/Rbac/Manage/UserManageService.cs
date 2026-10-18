using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Sys.Constants;
using Suanpan.Core.Sys.Entities;
using Suanpan.Core.Sys.Enums;
using Suanpan.Core.Sys.Rules;
using Suanpan.Data;
using Suanpan.Services.Sys.Auth;
using Suanpan.Services.Sys.Rbac.Manage.Models;
using Suanpan.Services.Sys.Security;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Sys.Rbac.Manage;

public sealed class UserManageService(
    SuanpanDbContext db,
    IPasswordHasher passwordHasher,
    IPermissionService permissionService,
    ISecurityStampValidator stampValidator,
    ICurrentUser currentUser) : IUserManageService
{
    public async Task<PagedResult<UserRow>> QueryAsync(UserQuery query, CancellationToken ct = default)
    {
        var users = db.SysUsers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var pattern = $"%{EscapeLike(query.Keyword.Trim())}%";
            // 显式指定转义字符：不指定时 Npgsql 会生成 ESCAPE ''，反斜杠不起作用
            users = users.Where(u => EF.Functions.ILike(u.Username, pattern, LikeEscape) || EF.Functions.ILike(u.RealName, pattern, LikeEscape));
        }

        if (query.Status is { } status)
        {
            users = users.Where(u => u.Status == status);
        }

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var total = await users.CountAsync(ct);
        var rows = await users.OrderBy(u => u.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new { u.Id, u.Username, u.RealName, u.Email, u.Status, u.LockedUntil, u.LastLoginAt, u.CreatedAt })
            .ToListAsync(ct);

        var userIds = rows.Select(r => r.Id).ToList();
        var roleNames = await (
            from ur in db.SysUserRoles
            join r in db.SysRoles on ur.RoleId equals r.Id
            where userIds.Contains(ur.UserId)
            orderby r.Id
            select new { ur.UserId, r.RoleName }
        ).ToListAsync(ct);

        var items = rows.Select(u => new UserRow(
            u.Id, u.Username, u.RealName, u.Email, u.Status, u.LockedUntil,
            roleNames.Where(x => x.UserId == u.Id).Select(x => x.RoleName).ToList(),
            u.LastLoginAt, u.CreatedAt)).ToList();
        return new PagedResult<UserRow>(items, total, page, pageSize);
    }

    public async Task<UserDetail?> GetAsync(long userId, CancellationToken ct = default)
    {
        var user = await db.SysUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return null;
        }

        var roleIds = await db.SysUserRoles.Where(x => x.UserId == userId).Select(x => x.RoleId).ToListAsync(ct);
        return new UserDetail(user.Id, user.Username, user.RealName, user.Email, user.Status, roleIds);
    }

    public async Task<ManageResult> CreateAsync(UserCreateInput input, CancellationToken ct = default)
    {
        var username = input.Username.Trim();
        if (username.Length == 0)
        {
            return ManageResult.Fail("登录名不能为空");
        }

        if (await db.SysUsers.AnyAsync(u => u.Username == username, ct))
        {
            return ManageResult.Fail($"登录名 {username} 已存在");
        }

        if (PasswordPolicy.Validate(input.Password) is { } passwordError)
        {
            return ManageResult.Fail(passwordError);
        }

        var roleIds = input.RoleIds.Distinct().ToList();
        if (await ValidateRolesAsync(roleIds, ct) is { } roleError)
        {
            return ManageResult.Fail(roleError);
        }

        var user = new SysUser
        {
            Username = username,
            RealName = input.RealName.Trim(),
            Email = NullIfBlank(input.Email),
            Status = UserStatus.Pending,
            MustChangePassword = true,
            PasswordHash = passwordHasher.Hash(input.Password),
        };

        await db.InTransactionAsync(async () =>
        {
            db.SysUsers.Add(user);
            await db.SaveChangesAsync(ct);
            db.SysUserRoles.AddRange(roleIds.Select(id => new SysUserRole { UserId = user.Id, RoleId = id }));
            await db.SaveChangesAsync(ct);
        }, ct);

        return ManageResult.Ok;
    }

    public async Task<ManageResult> UpdateAsync(UserUpdateInput input, CancellationToken ct = default)
    {
        var user = await db.SysUsers.FirstOrDefaultAsync(u => u.Id == input.Id, ct);
        if (user is null)
        {
            return ManageResult.Fail("账号不存在");
        }

        var roleIds = input.RoleIds.Distinct().ToList();
        if (await ValidateRolesAsync(roleIds, ct) is { } roleError)
        {
            return ManageResult.Fail(roleError);
        }

        var adminRoleId = await AdminRoleIdAsync(ct);
        var isAdmin = await db.SysUserRoles.AnyAsync(x => x.UserId == user.Id && x.RoleId == adminRoleId, ct);
        if (isAdmin && !roleIds.Contains(adminRoleId)
            && UserStatusRule.CanRemoveAdminRole(user.Status != UserStatus.Disabled, await CountActiveAdminsAsync(adminRoleId, ct)) is { } adminError)
        {
            return ManageResult.Fail(adminError);
        }

        user.RealName = input.RealName.Trim();
        user.Email = NullIfBlank(input.Email);

        await db.InTransactionAsync(async () =>
        {
            await db.SysUserRoles.Where(x => x.UserId == user.Id).ExecuteDeleteAsync(ct);
            db.SysUserRoles.AddRange(roleIds.Select(id => new SysUserRole { UserId = user.Id, RoleId = id }));
            await db.SaveChangesAsync(ct);
        }, ct);

        permissionService.Invalidate(user.Id);
        return ManageResult.Ok;
    }

    public async Task<ManageResult> SetStatusAsync(long userId, UserStatus status, CancellationToken ct = default)
    {
        if (status == UserStatus.Pending)
        {
            return ManageResult.Fail("不能手工设为待激活");
        }

        var user = await db.SysUsers.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return ManageResult.Fail("账号不存在");
        }

        if (user.Status == status)
        {
            return ManageResult.Ok;
        }

        if (status == UserStatus.Disabled)
        {
            var adminRoleId = await AdminRoleIdAsync(ct);
            var isAdmin = await db.SysUserRoles.AnyAsync(x => x.UserId == userId && x.RoleId == adminRoleId, ct);
            var error = UserStatusRule.CanDisable(userId, currentUser.UserId, isAdmin, await CountActiveAdminsAsync(adminRoleId, ct));
            if (error is not null)
            {
                return ManageResult.Fail(error);
            }

            // 刷新安全戳：该账号已登录的会话在下一个请求时被注销
            user.SecurityStamp = NewSecurityStamp();
        }
        else if (user.Status != UserStatus.Disabled)
        {
            return ManageResult.Fail("待激活的账号由本人首次登录完成激活");
        }

        user.Status = status;
        await db.SaveChangesAsync(ct);

        permissionService.Invalidate(userId);
        stampValidator.Invalidate(userId);
        return ManageResult.Ok;
    }

    public async Task<ManageResult> ResetPasswordAsync(long userId, string newPassword, CancellationToken ct = default)
    {
        if (PasswordPolicy.Validate(newPassword) is { } passwordError)
        {
            return ManageResult.Fail(passwordError);
        }

        var user = await db.SysUsers.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return ManageResult.Fail("账号不存在");
        }

        user.PasswordHash = passwordHasher.Hash(newPassword);
        user.MustChangePassword = true;
        user.SecurityStamp = NewSecurityStamp();
        await db.SaveChangesAsync(ct);

        stampValidator.Invalidate(userId);
        return ManageResult.Ok;
    }

    public async Task<ManageResult> ResetTotpAsync(long userId, CancellationToken ct = default)
    {
        var user = await db.SysUsers.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return ManageResult.Fail("账号不存在");
        }

        if (user.TotpSecret is null)
        {
            return ManageResult.Fail("该账号还没有绑定二次验证");
        }

        user.TotpSecret = null;
        user.SecurityStamp = NewSecurityStamp();
        await db.SaveChangesAsync(ct);

        stampValidator.Invalidate(userId);
        return ManageResult.Ok;
    }

    private async Task<string?> ValidateRolesAsync(IReadOnlyCollection<long> roleIds, CancellationToken ct)
    {
        if (roleIds.Count == 0)
        {
            return null;
        }

        var found = await db.SysRoles.CountAsync(r => roleIds.Contains(r.Id), ct);
        return found == roleIds.Count ? null : "提交的角色包含无效项，请刷新后重试";
    }

    private Task<long> AdminRoleIdAsync(CancellationToken ct)
        => db.SysRoles.Where(r => r.RoleCode == RoleCodes.SuperAdmin).Select(r => r.Id).SingleAsync(ct);

    /// <summary>未停用（待激活或启用）且持有管理员角色的账号数</summary>
    private Task<int> CountActiveAdminsAsync(long adminRoleId, CancellationToken ct)
        => (from ur in db.SysUserRoles
            join u in db.SysUsers on ur.UserId equals u.Id
            where ur.RoleId == adminRoleId && u.Status != UserStatus.Disabled
            select u.Id).CountAsync(ct);

    private static string NewSecurityStamp() => Guid.NewGuid().ToString("N");

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private const string LikeEscape = @"\";

    /// <summary>转义 LIKE 中的通配符，按普通字符匹配</summary>
    private static string EscapeLike(string value)
        => value.Replace(LikeEscape, LikeEscape + LikeEscape).Replace("%", LikeEscape + "%").Replace("_", LikeEscape + "_");
}
