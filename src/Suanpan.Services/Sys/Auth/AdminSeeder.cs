using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Suanpan.Core.Sys.Constants;
using Suanpan.Core.Sys.Entities;
using Suanpan.Core.Sys.Enums;
using Suanpan.Data;
using Suanpan.Services.Sys.Security;

namespace Suanpan.Services.Sys.Auth;

/// <summary>
/// 初始管理员（sys 设计 §3.4）：仅当 sys_user 为空时创建。登录名和初始密码读配置 Seed:AdminUsername、Seed:AdminPassword，
/// 未设置时跳过并记 Error 日志。账号为待激活、绑定管理员角色，首次登录必须改密码并绑定 TOTP。
/// </summary>
public sealed class AdminSeeder(SuanpanDbContext db, IPasswordHasher passwordHasher, IConfiguration configuration, ILogger<AdminSeeder> logger)
{
    /// <summary>返回是否创建了初始管理员</summary>
    public async Task<bool> SeedAsync(CancellationToken ct = default)
    {
        if (await db.SysUsers.AnyAsync(ct))
        {
            return false;
        }

        var username = configuration["Seed:AdminUsername"]?.Trim();
        var password = configuration["Seed:AdminPassword"];
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            logger.LogError("还没有任何后台账号，但没有设置初始管理员（Seed:AdminUsername、Seed:AdminPassword），跳过创建");
            return false;
        }

        var adminRoleId = await db.SysRoles.Where(r => r.RoleCode == RoleCodes.SuperAdmin).Select(r => r.Id).SingleAsync(ct);
        var admin = new SysUser
        {
            Username = username,
            RealName = "管理员",
            Status = UserStatus.Pending,
            MustChangePassword = true,
            PasswordHash = passwordHasher.Hash(password),
        };

        await db.InTransactionAsync(async () =>
        {
            db.SysUsers.Add(admin);
            await db.SaveChangesAsync(ct);
            db.SysUserRoles.Add(new SysUserRole { UserId = admin.Id, RoleId = adminRoleId });
            await db.SaveChangesAsync(ct);
        }, ct);

        logger.LogInformation("已创建初始管理员 {Username}：首次登录须改密码并绑定二次验证", username);
        return true;
    }
}
