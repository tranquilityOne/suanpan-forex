using Microsoft.Extensions.Logging;
using Suanpan.Infrastructure.Redis;

namespace Suanpan.Services.Sys.Auth;

/// <summary>
/// TOTP 防重放（sys 设计 §5.4）：同一账号、同一个时间步的验证码只能用一次，用 Redis SETNX 标记。
/// Redis 不可用时无法确认验证码是否用过，按拒绝处理（fail-closed）。
/// </summary>
public sealed class TotpReplayGuard(IRedisCache cache, ILogger<TotpReplayGuard> logger)
{
    /// <summary>验证码在前后各 1 个时间步（共 90 秒）内有效，标记保留 3 分钟足够覆盖</summary>
    private static readonly TimeSpan MarkerTtl = TimeSpan.FromMinutes(3);

    /// <summary>标记为已使用：首次使用返回 true，已用过返回 false，Redis 不可用返回 null</summary>
    public async Task<bool?> TryUseAsync(long userId, long timeStep)
    {
        try
        {
            return await cache.SetIfNotExistsAsync($"totp:used:{userId}:{timeStep}", 1, MarkerTtl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TOTP 防重放检查失败（Redis 不可用），拒绝本次验证：账号 {UserId}", userId);
            return null;
        }
    }
}
