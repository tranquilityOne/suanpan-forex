using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Suanpan.Shared.Messages;

namespace Suanpan.Infrastructure.Messaging;

public sealed class RabbitMqEventBus(RabbitMqConnection connection, ILogger<RabbitMqEventBus> logger) : IEventBus, IAsyncDisposable
{
    // 通道不是线程安全的：发布端用一个通道，用信号量串行发布
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<string> _declaredDelayQueues = [];
    private IChannel? _channel;

    public async Task PublishAsync<TMessage>(TMessage message, TimeSpan? delay = null, CancellationToken ct = default)
        where TMessage : IntegrationMessage
    {
        var messageName = message.MessageName;
        var body = JsonSerializer.SerializeToUtf8Bytes(message, message.GetType());
        var properties = new BasicProperties { Persistent = true, ContentType = "application/json" };

        await _gate.WaitAsync(ct);
        try
        {
            _channel ??= await connection.CreateChannelAsync(ct);

            if (delay is not { } wait || wait <= TimeSpan.Zero)
            {
                await _channel.BasicPublishAsync(MessageQueueNames.Exchange, messageName, mandatory: false, properties, body, ct);
            }
            else
            {
                // 经默认交换机直接投进延时队列
                var delayQueue = await EnsureDelayQueueAsync(_channel, messageName, wait, ct);
                await _channel.BasicPublishAsync(string.Empty, delayQueue, mandatory: false, properties, body, ct);
            }
        }
        finally
        {
            _gate.Release();
        }

        logger.LogDebug("消息已发布：{MessageName}，延时 {Delay}", messageName, delay);
    }

    /// <summary>延时队列：TTL 到期后转到公共交换机，按原路由键分发。同一队列内 TTL 相同，不会队头阻塞</summary>
    private async Task<string> EnsureDelayQueueAsync(IChannel channel, string messageName, TimeSpan delay, CancellationToken ct)
    {
        var seconds = (long)Math.Ceiling(delay.TotalSeconds);
        var queue = MessageQueueNames.Delay(messageName, seconds);
        if (_declaredDelayQueues.Contains(queue))
        {
            return queue;
        }

        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-message-ttl"] = seconds * 1000,
                ["x-dead-letter-exchange"] = MessageQueueNames.Exchange,
                ["x-dead-letter-routing-key"] = messageName,
            }, cancellationToken: ct);

        _declaredDelayQueues.Add(queue);
        return queue;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        _gate.Dispose();
    }
}
