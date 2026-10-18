using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Suanpan.Infrastructure.Messaging;

/// <summary>
/// RabbitMQ 连接（单例）：第一次使用时才连接，断线后由客户端自动恢复。
/// 连接全局一份；通道由发布端、接收端各自创建。
/// </summary>
public sealed class RabbitMqConnection(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConnection> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public RabbitMqOptions Options { get; } = options.Value;

    /// <summary>创建通道，并确保公共交换机存在</summary>
    public async Task<IChannel> CreateChannelAsync(CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);
        var channel = await connection.CreateChannelAsync(cancellationToken: ct);
        await channel.ExchangeDeclareAsync(MessageQueueNames.Exchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: ct);
        return channel;
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            var factory = new ConnectionFactory
            {
                Uri = new Uri(Options.Uri),
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
            };
            _connection = await factory.CreateConnectionAsync($"suanpan-{Options.AppName}", ct);
            logger.LogInformation("RabbitMQ 已连接：{Host}/{VirtualHost}", factory.HostName, factory.VirtualHost);
            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _gate.Dispose();
    }
}
