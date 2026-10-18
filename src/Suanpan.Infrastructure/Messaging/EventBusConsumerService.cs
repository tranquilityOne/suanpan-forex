using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Suanpan.Shared.Messages;

namespace Suanpan.Infrastructure.Messaging;

/// <summary>
/// 接收端：为本程序订阅的每种消息声明接收队列（suanpan.{程序}.{消息名}）并接收。
/// 收到后反序列化，在新的 DI 作用域中依次调用该消息的所有处理器。
/// 失败时：重试次数未用完，经重试队列延迟后回到本程序的接收队列；用完后转入 .failed 队列等待人工处理。
/// 原消息总是确认（ack），不会无限重新入队。没有订阅时直接退出。
/// </summary>
public sealed class EventBusConsumerService(
    RabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    IEnumerable<MessageSubscription> subscriptions,
    ILogger<EventBusConsumerService> logger) : BackgroundService
{
    private const string RetryHeader = "x-retry";

    private static readonly TimeSpan StartRetryInterval = TimeSpan.FromSeconds(10);

    private static readonly MethodInfo DispatchMethod =
        typeof(EventBusConsumerService).GetMethod(nameof(DispatchAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly IReadOnlyList<Type> _messageTypes = subscriptions.Select(s => s.MessageType).Distinct().ToList();
    private readonly HashSet<string> _declaredRetryQueues = [];
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_messageTypes.Count == 0)
        {
            return;
        }

        // 连不上时按固定间隔重试，不让程序启动失败
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await StartConsumingAsync(stoppingToken);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "RabbitMQ 接收端启动失败，{Seconds} 秒后重试", StartRetryInterval.TotalSeconds);
                await CloseChannelAsync();
                await Task.Delay(StartRetryInterval, stoppingToken);
            }
        }
    }

    private async Task StartConsumingAsync(CancellationToken ct)
    {
        var appName = connection.Options.AppName;
        _channel = await connection.CreateChannelAsync(ct);
        await _channel.BasicQosAsync(0, prefetchCount: 10, global: false, ct);

        foreach (var messageType in _messageTypes)
        {
            var messageName = messageType.Name;
            var queue = MessageQueueNames.Queue(appName, messageName);
            await _channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
            await _channel.QueueDeclareAsync(MessageQueueNames.Failed(queue), durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
            await _channel.QueueBindAsync(queue, MessageQueueNames.Exchange, messageName, cancellationToken: ct);

            var dispatch = DispatchMethod.MakeGenericMethod(messageType)
                .CreateDelegate<Func<IServiceProvider, byte[], CancellationToken, Task>>();
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += (_, args) => OnReceivedAsync(dispatch, messageName, queue, args, ct);
            await _channel.BasicConsumeAsync(queue, autoAck: false, consumer, ct);

            logger.LogInformation("已订阅消息 {MessageName}，队列 {Queue}", messageName, queue);
        }
    }

    private async Task OnReceivedAsync(
        Func<IServiceProvider, byte[], CancellationToken, Task> dispatch,
        string messageName,
        string queue,
        BasicDeliverEventArgs args,
        CancellationToken ct)
    {
        // Body 只在回调期间有效，先复制
        var body = args.Body.ToArray();
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await dispatch(scope.ServiceProvider, body, ct);
        }
        catch (Exception ex)
        {
            await HandleFailureAsync(ex, messageName, queue, body, RetriedCount(args.BasicProperties), ct);
        }

        await _channel!.BasicAckAsync(args.DeliveryTag, multiple: false, ct);
    }

    private async Task HandleFailureAsync(Exception ex, string messageName, string queue, byte[] body, int retried, CancellationToken ct)
    {
        var options = connection.Options;
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            Headers = new Dictionary<string, object?> { [RetryHeader] = retried + 1 },
        };

        if (retried < options.MaxRetryCount)
        {
            logger.LogWarning(ex, "消息处理失败：{MessageName}，{Delay} 秒后第 {Retry}/{Max} 次重试",
                messageName, options.RetryDelaySeconds, retried + 1, options.MaxRetryCount);
            var retryQueue = await EnsureRetryQueueAsync(queue, options.RetryDelaySeconds, ct);
            await _channel!.BasicPublishAsync(string.Empty, retryQueue, mandatory: false, properties, body, ct);
        }
        else
        {
            var failedQueue = MessageQueueNames.Failed(queue);
            logger.LogError(ex, "消息处理失败，已重试 {Max} 次，转入 {FailedQueue} 等待人工处理：{Body}",
                options.MaxRetryCount, failedQueue, Encoding.UTF8.GetString(body));
            await _channel!.BasicPublishAsync(string.Empty, failedQueue, mandatory: false, properties, body, ct);
        }
    }

    /// <summary>重试队列：TTL 到期后经默认交换机回到本程序的接收队列，不会再分发给其他程序</summary>
    private async Task<string> EnsureRetryQueueAsync(string queue, int delaySeconds, CancellationToken ct)
    {
        var retryQueue = MessageQueueNames.Retry(queue, delaySeconds);
        if (_declaredRetryQueues.Add(retryQueue))
        {
            await _channel!.QueueDeclareAsync(retryQueue, durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-message-ttl"] = delaySeconds * 1000,
                    ["x-dead-letter-exchange"] = string.Empty,
                    ["x-dead-letter-routing-key"] = queue,
                }, cancellationToken: ct);
        }

        return retryQueue;
    }

    private static async Task DispatchAsync<TMessage>(IServiceProvider services, byte[] body, CancellationToken ct)
        where TMessage : IntegrationMessage
    {
        var message = JsonSerializer.Deserialize<TMessage>(body)
                      ?? throw new InvalidOperationException($"消息 {typeof(TMessage).Name} 的内容为空");

        foreach (var handler in services.GetServices<IIntegrationEventHandler<TMessage>>())
        {
            await handler.HandleAsync(message, ct);
        }
    }

    private static int RetriedCount(IReadOnlyBasicProperties properties)
        => properties.Headers?.TryGetValue(RetryHeader, out var value) == true
            ? value switch { int count => count, long count => (int)count, _ => 0 }
            : 0;

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await CloseChannelAsync();
    }

    private async Task CloseChannelAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }
    }
}
