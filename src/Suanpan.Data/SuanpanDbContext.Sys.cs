using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Sys.Entities;

namespace Suanpan.Data;

/// <summary>系统模块：账号、角色、权限点、系统配置、IP 白名单、操作日志、登录日志</summary>
public partial class SuanpanDbContext
{
    public DbSet<SysUser> SysUsers => Set<SysUser>();

    public DbSet<SysRole> SysRoles => Set<SysRole>();

    public DbSet<SysUserRole> SysUserRoles => Set<SysUserRole>();

    public DbSet<SysPermission> SysPermissions => Set<SysPermission>();

    public DbSet<SysRolePermission> SysRolePermissions => Set<SysRolePermission>();

    public DbSet<SysConfig> SysConfigs => Set<SysConfig>();

    public DbSet<SysIpWhitelist> SysIpWhitelists => Set<SysIpWhitelist>();

    public DbSet<SysOpLog> SysOpLogs => Set<SysOpLog>();

    public DbSet<SysLoginLog> SysLoginLogs => Set<SysLoginLog>();
}
