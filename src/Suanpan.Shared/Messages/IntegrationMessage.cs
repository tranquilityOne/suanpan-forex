using System.Text.Json.Serialization;

namespace Suanpan.Shared.Messages;

/// <summary>
/// 跨进程消息的基类，经 RabbitMQ 传递（docs/conventions/backend.md §8.2）。
/// 具体消息放在 Messages/{模块}/ 下，用 record 定义，只放接收方需要的最少字段：
/// 事件是已经发生的事实，用过去式并以 Event 结尾（EventReleasedEvent）；指令要求对方做一件事，以 Command 结尾（ReparseCommand）。
/// 放在 Shared 中，Portal 不引用 Services 也能接收。
/// </summary>
public abstract record IntegrationMessage
{
    /// <summary>消息唯一 id，接收方可以用它去重（消费端必须幂等）</summary>
    public Guid MessageId { get; init; } = Guid.NewGuid();

    /// <summary>消息产生的时间（UTC）</summary>
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    /// <summary>路由键：消息类型名，同名消息进同一组队列</summary>
    [JsonIgnore]
    public string MessageName => GetType().Name;
}
