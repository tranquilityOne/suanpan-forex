using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Suanpan.Services.Common.Alerts;
using Suanpan.Services.Sys.Auth;
using Suanpan.Services.Sys.Configs;
using Suanpan.Services.Sys.Logging;
using Suanpan.Services.Sys.Rbac;
using Suanpan.Services.Sys.Rbac.Manage;
using Suanpan.Services.Sys.Security;
using Suanpan.Shared.Auth;

namespace Suanpan.Services;

public static class ServicesServiceCollectionExtensions
{
    /// <summary>
    /// 注册业务服务。调用方还需要注册 Data（AddSuanpanData）和 Redis（AddSuanpanRedis，登录的 TOTP 防重放等用到），
    /// 见 docs/conventions/backend.md §4。
    /// 权限树重建 <see cref="IPermissionRebuildService"/> 依赖 MVC、初始管理员的启动服务 <see cref="AdminSeedHostedService"/>，只由 Crm 注册。
    /// </summary>
    public static IServiceCollection AddSuanpanServices(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);

        services.AddHttpContextAccessor();
        services.TryAddSingleton<ICurrentUser, HttpContextCurrentUser>();

        // 告警：M1 只写 Error 日志，Ops 模块（FR-025）完成后在此之前注册真正的实现
        services.TryAddSingleton<IAlertService, LoggingAlertService>();

        // Sys：安全组件
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<ISecretProtector, AesSecretProtector>();
        services.AddScoped<IIpWhitelistService, IpWhitelistService>();

        // Sys：认证与授权
        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<TotpReplayGuard>();
        services.AddScoped<AdminSeeder>();
        services.AddScoped<ISecurityStampValidator, SecurityStampValidator>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<IPresetRoleGrantService, PresetRoleGrantService>();

        // Sys：账号、角色、权限点管理
        services.AddScoped<IUserManageService, UserManageService>();
        services.AddScoped<IRoleManageService, RoleManageService>();
        services.AddScoped<IPermissionManageService, PermissionManageService>();

        // Sys：日志与配置；OpLogActionFilter 由 Crm 注册为全局过滤器
        services.AddScoped<IOpLogService, OpLogService>();
        services.AddScoped<ILoginLogService, LoginLogService>();
        services.AddScoped<OpLogActionFilter>();
        services.AddScoped<IConfigService, ConfigService>();

        return services;
    }
}
