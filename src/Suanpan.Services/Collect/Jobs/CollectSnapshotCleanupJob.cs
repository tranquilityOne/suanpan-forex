using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Suanpan.Data;
using Suanpan.Infrastructure.Redis;
using Suanpan.Services.Collect.Runs;
using Suanpan.Services.Collect.Snapshots;
using Suanpan.Services.Sys.Configs;

namespace Suanpan.Services.Collect.Jobs;

/// <summary>
/// 快照的清理（采集设计 §4.6，BR-022）：Job 中每天执行一次的周期任务 collect-snapshot-cleanup，分批删除：
/// <list type="number">
/// <item>早于保留天数（系统配置 collect:snapshot_retention_days，默认 180）、且没有被入库数据引用的快照</item>
/// <item>被引用的快照保留 1 年：早于 365 天和保留天数中较长者的快照</item>
/// <item>不再被任何快照引用的内容</item>
/// </list>
/// </summary>
/// <param name="batchSize">每批删除的条数；测试中改小</param>
public sealed class CollectSnapshotCleanupJob(
    SuanpanDbContext db,
    ISnapshotContentStore contentStore,
    IConfigService config,
    IDistributedLock distributedLock,
    TimeProvider timeProvider,
    ILogger<CollectSnapshotCleanupJob> logger,
    int batchSize = CollectSnapshotCleanupJob.DefaultBatchSize)
{
    public const string JobId = "collect-snapshot-cleanup";

    /// <summary>每天 UTC 20:30，在运行记录的清理（20:00）之后</summary>
    public const string Cron = "30 20 * * *";

    public const int DefaultBatchSize = 5000;

    /// <summary>被入库数据引用的快照至少保留的天数（BR-022 的"≥ 1 年"）</summary>
    public const int ReferencedRetentionDays = 365;

    private const string LockKey = "collect:snapshot-cleanup";

    private static readonly TimeSpan LockExpiry = TimeSpan.FromHours(1);

    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(CancellationToken ct)
    {
        await using var handle = await distributedLock.AcquireAsync(LockKey, LockExpiry, ct: ct);
        if (handle is null)
        {
            logger.LogInformation("另一个实例正在清理快照，跳过本次");
            return;
        }

        var days = await config.GetIntAsync(CollectConfigKeys.SnapshotRetentionDays, CollectConfigKeys.SnapshotRetentionDaysDefault, ct);
        if (days < 1)
        {
            logger.LogWarning("系统配置 {Key} 为 {Days}，不是正整数，按默认的 {Default} 天清理",
                CollectConfigKeys.SnapshotRetentionDays, days, CollectConfigKeys.SnapshotRetentionDaysDefault);
            days = CollectConfigKeys.SnapshotRetentionDaysDefault;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var cutoff = now.AddDays(-days);
        var referencedCutoff = now.AddDays(-Math.Max(days, ReferencedRetentionDays));

        var snapshots = 0;
        int deleted;
        do
        {
            var batch = db.Snapshots
                .Where(s => (s.FetchedAt < cutoff && !s.Referenced) || s.FetchedAt < referencedCutoff)
                .OrderBy(s => s.Id)
                .Select(s => s.Id)
                .Take(batchSize);
            deleted = await db.Snapshots.Where(s => batch.Contains(s.Id)).ExecuteDeleteAsync(ct);
            snapshots += deleted;
        }
        while (deleted == batchSize);

        var contents = 0;
        do
        {
            deleted = await contentStore.DeleteOrphansAsync(batchSize, ct);
            contents += deleted;
        }
        while (deleted == batchSize);

        logger.LogInformation("已清理 {Snapshots} 个快照（未被引用的保留 {Days} 天）和 {Contents} 份不再使用的内容", snapshots, days, contents);
    }
}
