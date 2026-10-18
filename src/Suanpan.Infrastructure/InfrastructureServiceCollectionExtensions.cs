using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using Suanpan.Infrastructure.Messaging;
using Suanpan.Infrastructure.Redis;
using Suanpan.Shared.Messages;

namespace Suanpan.Infrastructure;

/// <summary>基础设施的注册方法，各程序按需组合（docs/conventions/backend.md §4）</summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>Hangfire 的表所在的 schema，由第一个迁移创建并授权给应用账号（数据库变更约定 §2.4）</summary>
    private const string HangfireSchema = "hangfire";

    /// <summary>
    /// 注册 Redis：缓存、分布式锁、限流器（连接串 Redis:Configuration）。
    /// Redis 暂时不可用不阻断程序启动，连上后自动恢复。
    /// </summary>
    public static IServiceCollection AddSuanpanRedis(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = GetRequired(configuration, "Redis:Configuration");

        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(connectionString);
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });
        services.TryAddSingleton<IRedisCache, RedisCache>();
        services.TryAddSingleton<IDistributedLock, RedisDistributedLock>();
        services.TryAddSingleton<IRateLimiter, RedisRateLimiter>();

        return services;
    }

    /// <summary>
    /// 注册 RabbitMQ 事件总线（连接串 RabbitMq:Uri）：发布端，以及接收端的后台服务（没有订阅时空转退出）。
    /// 接收的消息用 <see cref="AddIntegrationEventHandler{TMessage, THandler}"/> 登记。
    /// </summary>
    /// <param name="appName">程序名（crm、job、collector、portal），用作接收队列名 suanpan.{程序}.{消息名}</param>
    public static IServiceCollection AddSuanpanEventBus(this IServiceCollection services, IConfiguration configuration, string appName)
    {
        if (string.IsNullOrEmpty(appName) || !appName.All(char.IsAsciiLetterLower))
        {
            throw new ArgumentException("程序名只能由小写英文字母组成，如 job", nameof(appName));
        }

        GetRequired(configuration, "RabbitMq:Uri");
        var section = configuration.GetSection(RabbitMqOptions.SectionName);
        services.Configure<RabbitMqOptions>(options =>
        {
            section.Bind(options);
            options.AppName = appName;
        });
        services.TryAddSingleton<RabbitMqConnection>();
        services.TryAddSingleton<IEventBus, RabbitMqEventBus>();
        services.AddHostedService<EventBusConsumerService>();

        return services;
    }

    /// <summary>
    /// 订阅消息：THandler 继承 IntegrationEventHandler&lt;TMessage&gt;，按 Scoped 解析。
    /// 同一种消息可以登记多个处理器，按登记顺序依次执行。
    /// </summary>
    public static IServiceCollection AddIntegrationEventHandler<TMessage, THandler>(this IServiceCollection services)
        where TMessage : IntegrationMessage
        where THandler : class, IIntegrationEventHandler<TMessage>
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IIntegrationEventHandler<TMessage>, THandler>());
        services.AddSingleton(new MessageSubscription(typeof(TMessage), typeof(THandler)));
        return services;
    }

    /// <summary>
    /// 注册 Hangfire，存储在业务库（ConnectionStrings:Main）的 hangfire schema 中，表由 Hangfire 启动时自动建立和升级。
    /// </summary>
    /// <param name="runServer">只有 Job 传 true（执行任务）；Crm 只挂面板，传 false</param>
    public static IServiceCollection AddSuanpanHangfire(this IServiceCollection services, IConfiguration configuration, bool runServer)
    {
        var connectionString = GetRequired(configuration, "ConnectionStrings:Main");

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                options => options.UseNpgsqlConnection(connectionString),
                new PostgreSqlStorageOptions { SchemaName = HangfireSchema, PrepareSchemaIfNecessary = true }));

        if (runServer)
        {
            services.AddHangfireServer();
        }

        return services;
    }

    private static string GetRequired(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"缺少配置 {key}。本地用 user-secrets 设置，其他环境用环境变量 SUANPAN__{key.Replace(":", "__")}（见 README）。");
        }

        return value;
    }
}
