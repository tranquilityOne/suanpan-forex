using Suanpan.Collector.Handlers;
using Suanpan.Infrastructure;
using Suanpan.Shared.Messages.Collect;

namespace Suanpan.Collector.Execution;

public static class CollectorServiceCollectionExtensions
{
    /// <summary>
    /// 注册采集的执行框架（采集设计 §4.3、§5）：执行器、CollectRunCommand 的处理器、执行中断的回收，以及各数据源的适配器和解析器。
    /// 调用前先注册 Services、Redis、采集用的 HttpClient 和 EventBus（后端约定 §4）。
    /// 新增数据源时在这里登记它的适配器和解析器（ENG-009 起），解析器同时登记到 ParserCatalog
    /// </summary>
    public static IServiceCollection AddSuanpanCollectors(this IServiceCollection services)
    {
        services.AddScoped<CollectExecutor>();
        services.AddIntegrationEventHandler<CollectRunCommand, CollectRunCommandHandler>();

        // 回收服务注册为单例再作为后台服务启动，测试中可以直接调用 RecoverAsync
        services.AddSingleton<StaleRunRecovery>();
        services.AddHostedService(provider => provider.GetRequiredService<StaleRunRecovery>());

        return services;
    }
}
