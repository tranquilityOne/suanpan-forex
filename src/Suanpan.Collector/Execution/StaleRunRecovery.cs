using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Data;

namespace Suanpan.Collector.Execution;

/// <summary>
/// 执行中断的回收（采集设计 §4.3，ERR-COL-009）：进程崩溃或重试消息丢失时，运行记录会停在"运行中"，
/// 同一任务之后的运行都会因"上一次还没结束"被跳过。Collector 启动时和之后每 5 分钟检查一次，
/// 按 <see cref="CollectRunStatusRule.IsInterrupted"/> 把中断的记为失败。多个实例同时回收时由条件更新保证只改一次
/// </summary>
public sealed class StaleRunRecovery(IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<StaleRunRecovery> logger)
    : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
        do
        {
            try
            {
                await RecoverAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "执行中断的回收失败，{Minutes} 分钟后再试", Interval.TotalMinutes);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>把执行中断的运行记录记为失败，返回记录数</summary>
    public async Task<int> RecoverAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SuanpanDbContext>();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var running = await (
            from r in db.CollectRuns.AsNoTracking()
            where r.Status == CollectRunStatus.Running
            join s in db.DataSources on r.SourceId equals s.Id into sources
            from s in sources.DefaultIfEmpty()
            select new { Run = r, TimeoutSeconds = s == null ? DataSourceRule.DefaultTimeoutSeconds : s.TimeoutSeconds })
            .ToListAsync(ct);

        var recovered = 0;
        foreach (var item in running.Where(i => CollectRunStatusRule.IsInterrupted(i.Run, TimeSpan.FromSeconds(i.TimeoutSeconds), now)))
        {
            var run = item.Run;
            var message = run.NextAttemptAt is not null
                ? "执行中断：等待重试时没有收到下一次请求的指令"
                : "执行中断：请求开始后没有结束，可能是采集服务中途退出";

            // 条件更新：执行器在这期间写完了结果（状态或请求次数变了）就不再改
            recovered += await db.CollectRuns
                .Where(r => r.Id == run.Id && r.Status == CollectRunStatus.Running && r.AttemptCount == run.AttemptCount)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, CollectRunStatus.Failed)
                    .SetProperty(r => r.EndedAt, now)
                    .SetProperty(r => r.NextAttemptAt, (DateTime?)null)
                    .SetProperty(r => r.ErrorCode, CollectErrorCodes.Interrupted)
                    .SetProperty(r => r.ErrorMessage, message), ct);
        }

        if (recovered > 0)
        {
            // 告警由 FR-025 按 BR-129 的规则处理，这里先记 Error 日志
            logger.LogError("{Count} 条运行记录执行中断，已记为失败（{ErrorCode}）", recovered, CollectErrorCodes.Interrupted);
        }

        return recovered;
    }
}
