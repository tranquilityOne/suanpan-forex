using Suanpan.Collector.Execution;
using Suanpan.Infrastructure.Messaging;
using Suanpan.Shared.Messages.Collect;

namespace Suanpan.Collector.Handlers;

/// <summary>
/// 接收 CollectRunCommand（采集设计 §4.1、§7.3）：定时到点、手动触发、重试都发这条指令，交给 <see cref="CollectExecutor"/> 执行一次请求。
/// 幂等由执行器按运行记录的状态和请求次数保证
/// </summary>
public sealed class CollectRunCommandHandler(CollectExecutor executor) : IntegrationEventHandler<CollectRunCommand>
{
    public override Task HandleAsync(CollectRunCommand message, CancellationToken ct) => executor.ExecuteAsync(message, ct);
}
