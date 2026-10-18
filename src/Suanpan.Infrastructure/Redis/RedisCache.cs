using System.Text.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Suanpan.Infrastructure.Redis;

public sealed class RedisCache(IConnectionMultiplexer redis, IOptions<RedisOptions> options) : IRedisCache
{
    private readonly string _prefix = options.Value.InstanceName;

    private IDatabase Db => redis.GetDatabase();

    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await Db.StringGetAsync(Key(key));
        return value.IsNullOrEmpty ? default : JsonSerializer.Deserialize<T>(value.ToString());
    }

    public Task SetAsync<T>(string key, T value, TimeSpan expiry)
        => Db.StringSetAsync(Key(key), JsonSerializer.Serialize(value), expiry, When.Always);

    public Task<bool> SetIfNotExistsAsync<T>(string key, T value, TimeSpan expiry)
        => Db.StringSetAsync(Key(key), JsonSerializer.Serialize(value), expiry, When.NotExists);

    public Task<bool> RemoveAsync(string key) => Db.KeyDeleteAsync(Key(key));

    public Task<bool> ExistsAsync(string key) => Db.KeyExistsAsync(Key(key));

    public async Task<long> IncrementAsync(string key, TimeSpan expiryOnCreate)
    {
        var value = await Db.StringIncrementAsync(Key(key));
        if (value == 1)
        {
            await Db.KeyExpireAsync(Key(key), expiryOnCreate);
        }

        return value;
    }

    private RedisKey Key(string key) => _prefix + key;
}
