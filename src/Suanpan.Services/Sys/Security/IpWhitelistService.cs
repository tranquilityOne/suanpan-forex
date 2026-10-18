using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Suanpan.Core.Sys.Entities;
using Suanpan.Data;
using Suanpan.Shared.Mvc;

namespace Suanpan.Services.Sys.Security;

public sealed class IpWhitelistService(SuanpanDbContext db, IMemoryCache cache) : IIpWhitelistService
{
    /// <summary>缓存键（sys 设计 §6.5）</summary>
    public const string CacheKey = "ipwhitelist";

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    public async Task<IReadOnlyList<SysIpWhitelist>> GetListAsync(CancellationToken ct = default)
        => await db.SysIpWhitelists.AsNoTracking().OrderByDescending(x => x.Id).ToListAsync(ct);

    public Task<SysIpWhitelist?> GetAsync(long id, CancellationToken ct = default)
        => db.SysIpWhitelists.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<ManageResult> SaveAsync(SysIpWhitelist item, CancellationToken ct = default)
    {
        var ipValue = item.IpValue.Trim();
        if (IpWhitelistMatcher.Validate(ipValue) is { } formatError)
        {
            return ManageResult.Fail(formatError);
        }

        if (await db.SysIpWhitelists.AnyAsync(x => x.IpValue == ipValue && x.Id != item.Id, ct))
        {
            return ManageResult.Fail("该 IP 已存在");
        }

        var remark = string.IsNullOrWhiteSpace(item.Remark) ? null : item.Remark.Trim();
        if (item.Id > 0)
        {
            var existing = await db.SysIpWhitelists.FirstOrDefaultAsync(x => x.Id == item.Id, ct);
            if (existing is null)
            {
                return ManageResult.Fail("条目不存在");
            }

            existing.IpValue = ipValue;
            existing.Remark = remark;
            existing.IsEnabled = item.IsEnabled;
        }
        else
        {
            item.IpValue = ipValue;
            item.Remark = remark;
            db.SysIpWhitelists.Add(item);
        }

        await db.SaveChangesAsync(ct);
        cache.Remove(CacheKey);
        return ManageResult.Ok;
    }

    public async Task<ManageResult> DeleteAsync(long id, CancellationToken ct = default)
    {
        var deleted = await db.SysIpWhitelists.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
        if (deleted == 0)
        {
            return ManageResult.Fail("条目不存在");
        }

        cache.Remove(CacheKey);
        return ManageResult.Ok;
    }

    public async Task<bool> IsAllowedAsync(IPAddress clientIp, CancellationToken ct = default)
    {
        var entries = await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await db.SysIpWhitelists.AsNoTracking()
                .Where(x => x.IsEnabled)
                .Select(x => x.IpValue)
                .ToListAsync(ct);
        });

        return entries is not null && entries.Any(e => IpWhitelistMatcher.Matches(e, clientIp));
    }
}
