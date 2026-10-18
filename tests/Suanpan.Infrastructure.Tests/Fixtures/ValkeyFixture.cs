using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Suanpan.Infrastructure.Redis;
using Testcontainers.Redis;
using Xunit;

[assembly: AssemblyFixture(typeof(Suanpan.Infrastructure.Tests.Fixtures.ValkeyFixture))]

namespace Suanpan.Infrastructure.Tests.Fixtures;

/// <summary>
/// 测试用 Valkey 8.1（与本地开发、生产的服务端一致）：Testcontainers 启动临时容器，需要 Docker。
/// 整个测试程序集共用一个容器，在第一个用到它的测试中才启动（只跑单元测试时不启动）；
/// 每个测试用自己的随机键前缀，互不影响。
/// </summary>
public sealed class ValkeyFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder("valkey/valkey:8.1-alpine").Build();

    private readonly Lazy<Task<IConnectionMultiplexer>> _redis;

    public ValkeyFixture() => _redis = new Lazy<Task<IConnectionMultiplexer>>(StartAsync);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public Task<IConnectionMultiplexer> GetRedisAsync() => _redis.Value;

    /// <summary>带随机前缀的配置，前缀以冒号结尾</summary>
    public static IOptions<RedisOptions> UniqueOptions()
        => Options.Create(new RedisOptions { InstanceName = $"t{Guid.NewGuid():N}:" });

    private async Task<IConnectionMultiplexer> StartAsync()
    {
        await _container.StartAsync();
        return await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString());
    }

    public async ValueTask DisposeAsync()
    {
        if (_redis.IsValueCreated)
        {
            await (await _redis.Value).DisposeAsync();
            await _container.DisposeAsync();
        }
    }
}
