using Suanpan.Infrastructure.Redis;
using Suanpan.Infrastructure.Tests.Fixtures;
using Xunit;

namespace Suanpan.Infrastructure.Tests.Redis;

[Trait(TestCategory.Key, TestCategory.Integration)]
public class RedisCacheTests(ValkeyFixture valkey)
{
    [Fact]
    public async Task KeysArePrefixed_AndCountersExpire()
    {
        var redis = await valkey.GetRedisAsync();
        var options = ValkeyFixture.UniqueOptions();
        var cache = new RedisCache(redis, options);
        var db = redis.GetDatabase();
        var prefix = options.Value.InstanceName;

        Assert.True(await cache.SetIfNotExistsAsync("done", 1, TimeSpan.FromMinutes(1)));
        Assert.False(await cache.SetIfNotExistsAsync("done", 1, TimeSpan.FromMinutes(1)));
        Assert.True(await db.KeyExistsAsync(prefix + "done"));

        Assert.Equal(1, await cache.IncrementAsync("count", TimeSpan.FromMinutes(1)));
        Assert.Equal(2, await cache.IncrementAsync("count", TimeSpan.FromMinutes(1)));
        Assert.NotNull(await db.KeyTimeToLiveAsync(prefix + "count"));
    }
}
