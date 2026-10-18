using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Suanpan.Services.Sys.Security;

namespace Suanpan.Services;

public static class ServicesServiceCollectionExtensions
{
    /// <summary>
    /// 注册业务服务。调用方还需要注册 Data（AddSuanpanData）；用到 Redis 的服务加入后，
    /// 调用方必须同时注册 Redis（docs/conventions/backend.md §4）。
    /// </summary>
    public static IServiceCollection AddSuanpanServices(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);

        // Sys：安全组件
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<ISecretProtector, AesSecretProtector>();
        services.AddScoped<IIpWhitelistService, IpWhitelistService>();

        return services;
    }
}
