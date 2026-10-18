using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Suanpan.Services.Sys.Auth;
using Suanpan.Services.Sys.Rbac;
using Suanpan.Services.Sys.Rbac.Manage;
using Suanpan.Services.Sys.Security;
using Suanpan.Shared.Auth;

namespace Suanpan.Services;

public static class ServicesServiceCollectionExtensions
{
    /// <summary>
    /// 注册业务服务。调用方还需要注册 Data（AddSuanpanData）；用到 Redis 的服务加入后，
    /// 调用方必须同时注册 Redis（docs/conventions/backend.md §4）。
    /// 权限树重建 <see cref="IPermissionRebuildService"/> 依赖 MVC，只由 Crm 注册。
    /// </summary>
    public static IServiceCollection AddSuanpanServices(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);

        services.AddHttpContextAccessor();
        services.TryAddSingleton<ICurrentUser, HttpContextCurrentUser>();

        // Sys：安全组件
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<ISecretProtector, AesSecretProtector>();
        services.AddScoped<IIpWhitelistService, IpWhitelistService>();

        // Sys：认证与授权
        services.AddScoped<ISecurityStampValidator, SecurityStampValidator>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<IPresetRoleGrantService, PresetRoleGrantService>();

        // Sys：账号、角色、权限点管理
        services.AddScoped<IUserManageService, UserManageService>();
        services.AddScoped<IRoleManageService, RoleManageService>();
        services.AddScoped<IPermissionManageService, PermissionManageService>();

        return services;
    }
}
