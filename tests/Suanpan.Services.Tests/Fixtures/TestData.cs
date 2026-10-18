using Suanpan.Core.Sys.Entities;
using Suanpan.Core.Sys.Enums;
using Suanpan.Data;
using Suanpan.Shared.Auth;

namespace Suanpan.Services.Tests.Fixtures;

/// <summary>
/// 在测试事务中准备数据。名称都带随机后缀：测试并行执行，避免唯一索引在不同事务之间互相等待。
/// </summary>
internal static class TestData
{
    /// <summary>第一个迁移写入的预置角色 id（sys 设计 §3.4）</summary>
    public const long AdminRoleId = 1;
    public const long EngineerRoleId = 2;
    public const long EditorRoleId = 3;
    public const long ViewerRoleId = 4;

    public static string Unique(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..(prefix.Length + 13)];

    public static async Task<SysUser> AddUserAsync(
        SuanpanDbContext db, CancellationToken ct, UserStatus status = UserStatus.Active, params long[] roleIds)
    {
        var user = new SysUser
        {
            Username = Unique("user"),
            RealName = "测试账号",
            PasswordHash = "pbkdf2$1$AAAA$AAAA",
            Status = status,
            MustChangePassword = false,
        };
        db.SysUsers.Add(user);
        await db.SaveChangesAsync(ct);

        db.SysUserRoles.AddRange(roleIds.Select(id => new SysUserRole { UserId = user.Id, RoleId = id }));
        await db.SaveChangesAsync(ct);
        return user;
    }

    public static async Task<SysRole> AddRoleAsync(SuanpanDbContext db, CancellationToken ct, bool enabled = true)
    {
        var role = new SysRole { RoleCode = Unique("role"), RoleName = "测试角色", IsEnabled = enabled };
        db.SysRoles.Add(role);
        await db.SaveChangesAsync(ct);
        return role;
    }

    public static async Task<SysPermission> AddPermissionAsync(
        SuanpanDbContext db, CancellationToken ct, string code, PermType type = PermType.Button,
        long parentId = 0, bool enabled = true, int sort = 0, string? url = null)
    {
        var permission = new SysPermission
        {
            PermCode = code,
            PermName = code,
            PermType = type,
            ParentId = parentId,
            IsEnabled = enabled,
            Sort = sort,
            MenuUrl = url,
        };
        db.SysPermissions.Add(permission);
        await db.SaveChangesAsync(ct);
        return permission;
    }

    public static async Task GrantAsync(SuanpanDbContext db, CancellationToken ct, long roleId, params long[] permIds)
    {
        db.SysRolePermissions.AddRange(permIds.Select(id => new SysRolePermission { RoleId = roleId, PermId = id }));
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>固定的当前用户</summary>
internal sealed class StubCurrentUser(long? userId = null, string? userName = null) : ICurrentUser
{
    public long? UserId { get; } = userId;

    public string? UserName { get; } = userName;
}
