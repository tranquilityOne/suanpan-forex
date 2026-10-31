using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Suanpan.Infrastructure.Redis;

public sealed class RedisDistributedLock(IConnectionMultiplexer redis, IOptions<RedisOptions> options, TimeProvider timeProvider)
    : IDistributedLock
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>值等于自己的 token 才删除，不会误删锁过期后别人拿到的锁</summary>
    private const string ReleaseScript = """
        if redis.call('get', KEYS[1]) == ARGV[1] then
            return redis.call('del', KEYS[1])
        else
            return 0
        end
        """;

    /// <summary>值等于自己的 token 才续期</summary>
    private const string ExtendScript = """
        if redis.call('get', KEYS[1]) == ARGV[1] then
            return redis.call('pexpire', KEYS[1], ARGV[2])
        else
            return 0
        end
        """;

    private readonly string _prefix = options.Value.InstanceName + "lock:";

    public async Task<IDistributedLockHandle?> AcquireAsync(string key, TimeSpan expiry, TimeSpan? wait = null, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        RedisKey redisKey = _prefix + key;
        var token = Guid.NewGuid().ToString("N");
        var deadline = timeProvider.GetUtcNow() + (wait ?? TimeSpan.Zero);

        while (true)
        {
            if (await db.StringSetAsync(redisKey, token, expiry, When.NotExists))
            {
                return new LockHandle(db, redisKey, token);
            }

            if (timeProvider.GetUtcNow() >= deadline)
            {
                return null;
            }

            await Task.Delay(RetryInterval, timeProvider, ct);
        }
    }

    private sealed class LockHandle(IDatabase db, RedisKey key, string token) : IDistributedLockHandle
    {
        private int _released;

        public async Task<bool> ExtendAsync(TimeSpan expiry)
            => Volatile.Read(ref _released) == 0
                && (long)await db.ScriptEvaluateAsync(ExtendScript, [key], [token, (long)expiry.TotalMilliseconds]) == 1;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 1)
            {
                return;
            }

            await db.ScriptEvaluateAsync(ReleaseScript, [key], [token]);
        }
    }
}
