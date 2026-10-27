using Hangfire;
using Suanpan.Infrastructure.Hangfire;
using Suanpan.Services.Collect.Jobs;

namespace Suanpan.Job.Registry;

/// <summary>
/// Job 的周期任务注册表（后端约定 §8.3）：所有周期任务集中在这里登记，启动时注册或更新到 Hangfire；cron 一律按 UTC。
/// 任务本身写在 Services 中，带 [AutomaticRetry(Attempts = 0)] 和分布式锁
/// </summary>
public static class RecurringJobRegistry
{
    public static void RegisterRecurringJobs(this IRecurringJobManager jobs)
    {
        // 数据采集（采集设计 §4.2、§4.5）
        jobs.AddOrUpdateUtc<CollectDispatchJob>(CollectDispatchJob.JobId, job => job.RunAsync(CancellationToken.None), CollectDispatchJob.Cron);
        jobs.AddOrUpdateUtc<CollectRunCleanupJob>(CollectRunCleanupJob.JobId, job => job.RunAsync(CancellationToken.None), CollectRunCleanupJob.Cron);
    }
}
