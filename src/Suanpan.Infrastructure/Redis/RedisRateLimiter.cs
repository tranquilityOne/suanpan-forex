using Microsoft.Extensions.Logging;

namespace Suanpan.Infrastructure.Redis;

public sealed class RedisRateLimiter(IRedisCache cache, TimeProvider timeProvider, ILogger<RedisRateLimiter> logger) : IRateLimiter
{
    public async Task<bool> TryAcquireAsync(string key, int limit, TimeSpan window)
    {
        if (limit <= 0)
        {
            return true;
        }

        // 同一窗口内各实例算出同一个窗口号，命中同一个键；过期时间给 2 个窗口，防止各实例的时钟略有偏差
        var windowNo = timeProvider.GetUtcNow().UtcTicks / window.Ticks;
        try
        {
            var count = await cache.IncrementAsync($"rl:{key}:{windowNo}", window * 2);
            return count <= limit;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "限流器访问 Redis 失败，本次放行：{Key}", key);
            return true;
        }
    }
}
