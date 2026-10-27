using Suanpan.Infrastructure.Messaging;
using Suanpan.Shared.Messages;

namespace Suanpan.Services.Tests.Fixtures;

/// <summary>消息总线的替身：记下发布的消息，不连 RabbitMQ；收发本身在 Infrastructure.Tests 中测试</summary>
internal sealed class RecordingEventBus : IEventBus
{
    public List<IntegrationMessage> Published { get; } = [];

    /// <summary>为 true 时发布抛出异常，模拟 RabbitMQ 不可用</summary>
    public bool Fail { get; set; }

    public Task PublishAsync<TMessage>(TMessage message, TimeSpan? delay = null, CancellationToken ct = default)
        where TMessage : IntegrationMessage
    {
        if (Fail)
        {
            throw new InvalidOperationException("RabbitMQ 不可用（模拟）");
        }

        Published.Add(message);
        return Task.CompletedTask;
    }
}
