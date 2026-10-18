using RabbitMQ.Client;
using Suanpan.Infrastructure.Messaging;
using Testcontainers.RabbitMq;
using Xunit;

[assembly: AssemblyFixture(typeof(Suanpan.Infrastructure.Tests.Fixtures.RabbitMqFixture))]

namespace Suanpan.Infrastructure.Tests.Fixtures;

/// <summary>
/// 测试用 RabbitMQ 4.1：Testcontainers 启动临时容器，需要 Docker。整个测试程序集共用一个容器，
/// 在第一个用到它的测试中才启动（只跑单元测试时不启动）；每个测试用自己的消息类型和程序名，队列互不影响。
/// </summary>
public sealed class RabbitMqFixture : IAsyncLifetime
{
    private readonly RabbitMqContainer _container = new RabbitMqBuilder("rabbitmq:4.1-alpine").Build();

    private readonly Lazy<Task<IConnection>> _connection;

    public RabbitMqFixture() => _connection = new Lazy<Task<IConnection>>(StartAsync);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>AMQP URI（第一次调用时启动容器）</summary>
    public async Task<string> GetUriAsync()
    {
        await _connection.Value;
        return _container.GetConnectionString();
    }

    /// <summary>
    /// 预先声明接收队列并绑定。接收端在后台启动，测试先声明队列，消息就不会在接收端就绪之前因没有队列而丢失。
    /// </summary>
    public async Task DeclareQueueAsync(string appName, string messageName, CancellationToken ct)
    {
        await using var channel = await CreateChannelAsync(ct);
        var queue = MessageQueueNames.Queue(appName, messageName);
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync(queue, MessageQueueNames.Exchange, messageName, cancellationToken: ct);
    }

    public async Task<IChannel> CreateChannelAsync(CancellationToken ct)
    {
        var connection = await _connection.Value;
        var channel = await connection.CreateChannelAsync(cancellationToken: ct);
        await channel.ExchangeDeclareAsync(MessageQueueNames.Exchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: ct);
        return channel;
    }

    private async Task<IConnection> StartAsync()
    {
        await _container.StartAsync();
        return await new ConnectionFactory { Uri = new Uri(_container.GetConnectionString()) }.CreateConnectionAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection.IsValueCreated)
        {
            await (await _connection.Value).DisposeAsync();
            await _container.DisposeAsync();
        }
    }
}
