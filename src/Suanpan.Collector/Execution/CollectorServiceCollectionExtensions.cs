using Suanpan.Collector.Handlers;
using Suanpan.Collector.Sources.US.Bls;
using Suanpan.Collector.Watch;
using Suanpan.Infrastructure;
using Suanpan.Shared.Messages.Collect;

namespace Suanpan.Collector.Execution;

public static class CollectorServiceCollectionExtensions
{
    /// <summary>
    /// 注册采集的执行框架（采集设计 §4.3、§5）：执行器和 CollectRunCommand 的处理器、重新解析和 ReparseCommand 的处理器、
    /// 执行中断的回收、盯盘服务（§5.7），以及各数据源的适配器和解析器。
    /// 调用前先注册 Services、Redis、采集用的 HttpClient 和 EventBus（后端约定 §4）。
    /// 新增数据源时在这里登记它的适配器和解析器（ENG-009 起），解析器同时登记到 ParserCatalog
    /// </summary>
    public static IServiceCollection AddSuanpanCollectors(this IServiceCollection services)
    {
        services.AddScoped<CollectRunWriter>();
        services.AddScoped<CollectExecutor>();
        services.AddIntegrationEventHandler<CollectRunCommand, CollectRunCommandHandler>();
        services.AddScoped<ReparseExecutor>();
        services.AddIntegrationEventHandler<ReparseCommand, ReparseCommandHandler>();

        // 回收服务注册为单例再作为后台服务启动，测试中可以直接调用 RecoverAsync
        services.AddSingleton<StaleRunRecovery>();
        services.AddHostedService(provider => provider.GetRequiredService<StaleRunRecovery>());

        // 盯盘（FR-003，采集设计 §5.7）：持有 Redis 租约的实例盯盘。按类型注册，测试中去掉后台服务、自己按假时钟调用
        services.AddHostedService<WatchService>();

        // 美国劳工统计局（ENG-009.1）：发布日程（官网）和公布值（API）是两个数据源，各一个适配器
        services.AddSingleton<ICollectAdapter, BlsWebAdapter>();
        services.AddSingleton<IParser, BlsScheduleParser>();
        services.AddSingleton<ICollectAdapter, BlsApiAdapter>();
        services.AddSingleton<IParser, BlsReleaseParser>();

        return services;
    }
}
