using System.Collections.Concurrent;
using Suanpan.Infrastructure.Redis;

namespace Suanpan.Services.Tests.Fixtures;

/// <summary>内存中的 Redis 替身：只实现服务测试用到的 SetIfNotExists、Increment（不处理过期）；Redis 本身的行为在 Infrastructure.Tests 中测试</summary>
internal sealed class FakeRedisCache : IRedisCache
{
    private readonly ConcurrentDictionary<string, object?> _values = new();

    /// <summary>为 true 时所有操作抛出异常，模拟 Redis 不可用</summary>
    public bool Unavailable { get; init; }

    public Task<bool> SetIfNotExistsAsync<T>(string key, T value, TimeSpan expiry)
        => Unavailable ? throw new InvalidOperationException("Redis 不可用（模拟）") : Task.FromResult(_values.TryAdd(key, value));

    public Task<T?> GetAsync<T>(string key) => throw new NotSupportedException();

    public Task SetAsync<T>(string key, T value, TimeSpan expiry) => throw new NotSupportedException();

    public Task<bool> RemoveAsync(string key) => throw new NotSupportedException();

    public Task<bool> ExistsAsync(string key) => throw new NotSupportedException();

    public Task<long> IncrementAsync(string key, TimeSpan expiryOnCreate)
        => Unavailable
            ? throw new InvalidOperationException("Redis 不可用（模拟）")
            : Task.FromResult((long)_values.AddOrUpdate(key, 1L, (_, value) => (long)value! + 1)!);
}
