using Suanpan.Shared.Messages;

namespace Suanpan.Infrastructure.Messaging;

/// <summary>
/// 消息处理器基类：一种消息一个处理器类，继承并实现 HandleAsync，放在所属模块的 Handlers/ 目录下。
/// 按 Scoped 解析，可以注入 DbContext 和业务服务。
/// 抛出异常即处理失败，框架按配置延迟重试，次数用完转入 .failed 队列。
/// **必须幂等**（用 MessageId 或业务单号去重），因为重试必然带来重复投递。
/// </summary>
public abstract class IntegrationEventHandler<TMessage> : IIntegrationEventHandler<TMessage>
    where TMessage : IntegrationMessage
{
    public abstract Task HandleAsync(TMessage message, CancellationToken ct);
}
