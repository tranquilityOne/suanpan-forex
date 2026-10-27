using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Suanpan.Data;
using Suanpan.Infrastructure.Redis;
using Suanpan.Services.Collect.Runs;
using Suanpan.Services.Sys.Configs;

namespace Suanpan.Services.Collect.Jobs;

/// <summary>
/// 运行记录的清理（采集设计 §4.5，BR-011）：Job 中每天执行一次的周期任务 collect-run-cleanup，
/// 删除触发时间早于保留天数（系统配置 collect:run_retention_days，默认 90）的记录，分批删除，避免一次锁住大量行
/// </summary>
/// <param name="batchSize">每批删除的条数；测试中改小</param>
public sealed class CollectRunCleanupJob(
    SuanpanDbContext db,
    IConfigService config,
    IDistributedLock distributedLock,
    TimeProvider timeProvider,
    ILogger<CollectRunCleanupJob> logger,
    int batchSize = CollectRunCleanupJob.DefaultBatchSize)
{
    public const string JobId = "collect-run-cleanup";

    /// <summary>每天 UTC 20:00（北京时间 04:00，访问量最低的时段）</summary>
    public const string Cron = "0 20 * * *";

    public const int DefaultBatchSize = 5000;

    private const string LockKey = "collect:run-cleanup";

    private static readonly TimeSpan LockExpiry = TimeSpan.FromHours(1);

    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(CancellationToken ct)
    {
        await using var handle = await distributedLock.AcquireAsync(LockKey, LockExpiry, ct: ct);
        if (handle is null)
        {
            logger.LogInformation("另一个实例正在清理运行记录，跳过本次");
            return;
        }

        var days = await config.GetIntAsync(CollectConfigKeys.RunRetentionDays, CollectConfigKeys.RunRetentionDaysDefault, ct);
        if (days < 1)
        {
            logger.LogWarning("系统配置 {Key} 为 {Days}，不是正整数，按默认的 {Default} 天清理",
                CollectConfigKeys.RunRetentionDays, days, CollectConfigKeys.RunRetentionDaysDefault);
            days = CollectConfigKeys.RunRetentionDaysDefault;
        }

        var cutoff = timeProvider.GetUtcNow().UtcDateTime.AddDays(-days);
        var total = 0;
        int deleted;
        do
        {
            var batch = db.CollectRuns.Where(r => r.QueuedAt < cutoff).OrderBy(r => r.Id).Select(r => r.Id).Take(batchSize);
            deleted = await db.CollectRuns.Where(r => batch.Contains(r.Id)).ExecuteDeleteAsync(ct);
            total += deleted;
        }
        while (deleted == batchSize);

        logger.LogInformation("已清理 {Count} 条 {Days} 天前的运行记录", total, days);
    }
}
