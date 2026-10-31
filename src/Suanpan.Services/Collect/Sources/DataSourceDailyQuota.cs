using Microsoft.Extensions.Logging;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Rules;
using Suanpan.Core.Ops.Enums;
using Suanpan.Infrastructure.Redis;
using Suanpan.Services.Common.Alerts;

namespace Suanpan.Services.Collect.Sources;

public sealed class DataSourceDailyQuota(
    IRedisCache cache,
    IAlertService alerts,
    TimeProvider timeProvider,
    ILogger<DataSourceDailyQuota> logger) : IDataSourceDailyQuota
{
    /// <summary>计数键比一天多留 1 小时，跨日时不会因为时钟误差提前过期</summary>
    private static readonly TimeSpan KeyExpiry = TimeSpan.FromHours(25);

    public async Task<bool> TryConsumeAsync(DataSource source, CancellationToken ct = default)
    {
        if (source.DailyLimit is not { } limit)
        {
            return true;
        }

        var today = timeProvider.GetUtcNow().UtcDateTime;
        long count;
        try
        {
            count = await cache.IncrementAsync(DataSourceRateLimit.DailyKey(source.Id, today), KeyExpiry);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "数据源 {SourceId} 的每日请求计数失败，本次放行", source.Id);
            return true;
        }

        // 计数是原子递增，每个值只出现一次：等于阈值的那一次请求告警，当天不会重复
        if (count == DataSourceRule.DailyLimitAlertAt(limit) && count <= limit)
        {
            await RaiseAsync(source, $"今日（UTC {today:yyyy-MM-dd}）请求数已达每日上限的 90%（{count}/{limit}）", ct);
        }
        else if (count == limit + 1)
        {
            await RaiseAsync(source, $"今日（UTC {today:yyyy-MM-dd}）请求数已达每日上限 {limit}，当日不再请求（ERR-COL-010）", ct);
        }

        return count <= limit;
    }

    /// <summary>告警是附带动作，失败只记日志（后端约定 §11）</summary>
    private async Task RaiseAsync(DataSource source, string message, CancellationToken ct)
    {
        try
        {
            await alerts.RaiseAsync(AlertLevel.Important, source.Name, message, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "告警发送失败：{Subject} {Message}", source.Name, message);
        }
    }
}
