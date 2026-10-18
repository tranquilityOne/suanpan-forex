using Suanpan.Shared.Messages;

namespace Suanpan.Infrastructure.Messaging;

/// <summary>
/// 跨进程消息的发布端（RabbitMQ）。进程内的定时和批处理走 Hangfire，不走这里。
/// 发布不在业务事务内（不做 Outbox），靠接收方幂等和对账兜底（docs/conventions/backend.md §8.2）。
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// 发布消息。delay 有值时延时投递：消息先进入"消息类型 × 延时秒数"专用的 TTL 队列，到期后再分发，不依赖延时插件。
    /// </summary>
    Task PublishAsync<TMessage>(TMessage message, TimeSpan? delay = null, CancellationToken ct = default)
        where TMessage : IntegrationMessage;
}
