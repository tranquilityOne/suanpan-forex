using Suanpan.Collector.Execution;
using Suanpan.Infrastructure.Messaging;
using Suanpan.Shared.Messages.Collect;

namespace Suanpan.Collector.Handlers;

/// <summary>
/// 接收 ReparseCommand（采集设计 §5.6、§7.3）：后台发起重新解析时发这条指令，交给 <see cref="ReparseExecutor"/> 执行。
/// 幂等由执行器按运行记录的状态保证
/// </summary>
public sealed class ReparseCommandHandler(ReparseExecutor executor) : IntegrationEventHandler<ReparseCommand>
{
    public override Task HandleAsync(ReparseCommand message, CancellationToken ct) => executor.ExecuteAsync(message, ct);
}
