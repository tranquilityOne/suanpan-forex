using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Suanpan.Core.Sys.Enums;
using Suanpan.Data;

namespace Suanpan.Services.Sys.Auth;

public sealed class SecurityStampValidator(SuanpanDbContext db, IMemoryCache cache) : ISecurityStampValidator
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    /// <summary>缓存键（sys 设计 §6.5）</summary>
    public static string CacheKey(long userId) => $"stamp:{userId}";

    public async Task<bool> ValidateAsync(long userId, string? stamp, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(stamp))
        {
            return false;
        }

        // 缓存 null 表示账号不存在或已停用
        var current = await cache.GetOrCreateAsync(CacheKey(userId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await db.SysUsers.AsNoTracking()
                .Where(u => u.Id == userId && u.Status != UserStatus.Disabled)
                .Select(u => u.SecurityStamp)
                .FirstOrDefaultAsync(ct);
        });

        return current is not null && string.Equals(current, stamp, StringComparison.Ordinal);
    }

    public void Invalidate(long userId) => cache.Remove(CacheKey(userId));
}
