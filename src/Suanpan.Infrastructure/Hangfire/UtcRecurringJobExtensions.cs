using System.Linq.Expressions;
using Hangfire;

namespace Suanpan.Infrastructure.Hangfire;

/// <summary>
/// 周期任务注册：cron 一律按 UTC 解释（docs/conventions/backend.md §8.3、PRD NFR-122）。
/// Job 的任务注册表只通过这里注册，不直接调用 Hangfire 的 AddOrUpdate，也不传入其他时区。
/// </summary>
public static class UtcRecurringJobExtensions
{
    public static void AddOrUpdateUtc<TJob>(
        this IRecurringJobManager manager, string recurringJobId, Expression<Func<TJob, Task>> methodCall, string cronExpression)
        => manager.AddOrUpdate(recurringJobId, methodCall, cronExpression, new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
}
