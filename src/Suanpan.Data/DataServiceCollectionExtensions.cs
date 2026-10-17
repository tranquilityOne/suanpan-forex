using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Suanpan.Data.Interceptors;
using Suanpan.Data.PublicViews;

namespace Suanpan.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>
    /// 注册业务库上下文（连接串 ConnectionStrings:Main）和审计字段拦截器。
    /// </summary>
    /// <param name="auditActor">
    /// 没有登录用户时写入审计字段的程序名，如 collector、job。
    /// Crm 需要在调用本方法之前注册自己的 <see cref="IAuditActorProvider"/>（返回登录名）。
    /// </param>
    public static IServiceCollection AddSuanpanData(this IServiceCollection services, IConfiguration configuration, string auditActor)
    {
        var connectionString = GetRequiredConnectionString(configuration, "Main");

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddScoped<IAuditActorProvider>(_ => new FixedAuditActorProvider(auditActor));
        services.AddScoped<AuditInterceptor>();
        services.AddDbContext<SuanpanDbContext>((sp, options) => options
            .UseNpgsql(connectionString)
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));

        return services;
    }

    /// <summary>
    /// 注册前台只读上下文（连接串 ConnectionStrings:Portal），查询默认不跟踪。
    /// </summary>
    public static IServiceCollection AddSuanpanPortalRead(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = GetRequiredConnectionString(configuration, "Portal");

        services.AddDbContext<PortalReadDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        return services;
    }

    private static string GetRequiredConnectionString(IConfiguration configuration, string name)
    {
        var connectionString = configuration.GetConnectionString(name);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"缺少连接串 ConnectionStrings:{name}。本地用 user-secrets 设置，其他环境用环境变量 SUANPAN__ConnectionStrings__{name}（见 README）。");
        }

        return connectionString;
    }
}
