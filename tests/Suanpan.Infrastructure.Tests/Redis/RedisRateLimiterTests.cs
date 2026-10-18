using Microsoft.Extensions.Logging.Abstractions;
using Suanpan.Infrastructure.Redis;
using Suanpan.Infrastructure.Tests.Fixtures;
using Xunit;

namespace Suanpan.Infrastructure.Tests.Redis;

[Trait(TestCategory.Key, TestCategory.Integration)]
public class RedisRateLimiterTests(ValkeyFixture valkey)
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task AllowsUpToLimitPerWindow_AndResetsInNextWindow()
    {
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 10, 18, 1, 0, 0, TimeSpan.Zero));
        var limiter = new RedisRateLimiter(
            new RedisCache(await valkey.GetRedisAsync(), ValkeyFixture.UniqueOptions()), time, NullLogger<RedisRateLimiter>.Instance);

        Assert.True(await limiter.TryAcquireAsync("bls", 2, Window));
        Assert.True(await limiter.TryAcquireAsync("bls", 2, Window));
        Assert.False(await limiter.TryAcquireAsync("bls", 2, Window));
        Assert.True(await limiter.TryAcquireAsync("ecb", 2, Window));

        time.UtcNow += Window;
        Assert.True(await limiter.TryAcquireAsync("bls", 2, Window));
    }
}

public class RedisRateLimiterFailOpenTests
{
    [Fact]
    public async Task RedisFailure_LetsRequestThrough()
    {
        var limiter = new RedisRateLimiter(new FailingCache(), TimeProvider.System, NullLogger<RedisRateLimiter>.Instance);

        Assert.True(await limiter.TryAcquireAsync("bls", 1, TimeSpan.FromSeconds(1)));
    }

    private sealed class FailingCache : IRedisCache
    {
        public Task<T?> GetAsync<T>(string key) => throw new InvalidOperationException();

        public Task SetAsync<T>(string key, T value, TimeSpan expiry) => throw new InvalidOperationException();

        public Task<bool> SetIfNotExistsAsync<T>(string key, T value, TimeSpan expiry) => throw new InvalidOperationException();

        public Task<bool> RemoveAsync(string key) => throw new InvalidOperationException();

        public Task<bool> ExistsAsync(string key) => throw new InvalidOperationException();

        public Task<long> IncrementAsync(string key, TimeSpan expiryOnCreate) => throw new InvalidOperationException("Redis 不可用");
    }
}
