using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Data;
using Suanpan.Infrastructure.Redis;
using Suanpan.Services.Collect.Watch;
using Suanpan.Services.Collect.Watch.Models;

namespace Suanpan.Collector.Watch;

/// <summary>
/// 盯盘服务（采集设计 §5.7，FR-003，DEC-017）：Collector 中的常驻后台服务。先取得 Redis 租约 collect:watch（15 秒过期、每 5 秒续期），
/// 只有持有租约的实例盯盘；续期失败时停止全部盯盘，重新等租约，由接手的实例接着写原来的运行记录（ERR-COL-006）。
/// 持有租约期间立即计划一次、之后每分钟一次（<see cref="TickAsync"/>）：按计划启动各路盯盘（<see cref="WatchSession"/>），
/// 收尾窗口已结束、中断了的盯盘，告警不能盯盘的事件，补采延迟的事件
/// </summary>
public sealed class WatchService(IServiceScopeFactory scopeFactory, IDistributedLock distributedLock, TimeProvider timeProvider, ILogger<WatchService> logger)
    : BackgroundService
{
    public const string LeaseKey = "collect:watch";

    public static readonly TimeSpan LeaseExpiry = TimeSpan.FromSeconds(15);

    public static readonly TimeSpan RenewInterval = TimeSpan.FromSeconds(5);

    public static readonly TimeSpan PlanInterval = TimeSpan.FromMinutes(1);

    /// <summary>心跳超过这么久、又不归任何进行中的盯盘的运行记录，算中断（ERR-COL-009）</summary>
    public static readonly TimeSpan OrphanGrace = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<(long TaskId, DateTime ScheduledAt), (WatchSession Session, Task Running)> _sessions = new();

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            IDistributedLockHandle? lease;
            try
            {
                lease = await distributedLock.AcquireAsync(LeaseKey, LeaseExpiry, ct: stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "取盯盘租约失败，{Seconds} 秒后再试", RenewInterval.TotalSeconds);
                lease = null;
            }

            if (lease is null)
            {
                await Task.Delay(RenewInterval, timeProvider, stoppingToken);
                continue;
            }

            await using (lease)
            {
                logger.LogInformation("取得盯盘租约，开始盯盘");
                using var leading = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                var renewing = RenewAsync(lease, leading);
                try
                {
                    await LeadAsync(leading.Token);
                }
                catch (OperationCanceledException) when (leading.IsCancellationRequested)
                {
                }
                finally
                {
                    await leading.CancelAsync();
                    await StopSessionsAsync();
                    await renewing;
                }
            }
        }
    }

    /// <summary>
    /// 计划一次（采集设计 §5.7）：总开关关闭时什么也不做（进行中的盯盘自己会停）。取最近两小时到未来的计划，
    /// 开始时间在计划时长内的启动；窗口已经结束的收尾；中断了的运行记录记为失败；不能盯盘的告警；补采延迟的事件
    /// </summary>
    public async Task TickAsync(CancellationToken ct)
    {
        foreach (var (key, entry) in _sessions)
        {
            if (entry.Running.IsCompleted)
            {
                _sessions.TryRemove(key, out _);
            }
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var plans = scope.ServiceProvider.GetRequiredService<IWatchPlanService>();
        var settings = await plans.GetSettingsAsync(ct);
        if (!settings.Enabled)
        {
            return;
        }

        var now = Now;
        var plan = await plans.GetPlanAsync(
            now - TimeSpan.FromMinutes(WatchRule.MaxWindowMinutes),
            now + TimeSpan.FromMinutes(WatchRule.MaxPlanMinutes) + TimeSpan.FromSeconds(WatchRule.MaxLeadSeconds), ct);
        var maxLead = plan.Groups.Count > 0 ? plan.Groups.Max(g => g.Timing.Lead) : TimeSpan.FromSeconds(settings.Defaults.LeadSeconds);
        var horizon = now + TimeSpan.FromMinutes(WatchRule.PlanMinutes(settings.PlanMinutes, maxLead));

        foreach (var group in plan.Groups.Where(g => g.Window.Start <= horizon))
        {
            var key = (group.TaskId, group.ScheduledAt);
            if (_sessions.ContainsKey(key))
            {
                continue;
            }

            var session = new WatchSession(group, scopeFactory, timeProvider, logger);
            if (group.Window.End <= now)
            {
                // 窗口在服务中断期间结束了：建（或接着用）运行记录，没拿到值的改为延迟，之后按延迟补采
                await FinishMissedAsync(session, ct);
                continue;
            }

            _sessions[key] = (session, Task.Run(() => RunSessionAsync(session, ct), CancellationToken.None));
        }

        await plans.AlertUnreadyAsync(plan.Problems.Where(p => p.ScheduledAt > now && p.ScheduledAt <= horizon), ct);
        await RecoverOrphansAsync(scope.ServiceProvider.GetRequiredService<SuanpanDbContext>(), now, ct);
        await scope.ServiceProvider.GetRequiredService<IWatchBackfillService>().RunAsync(settings.Backfill, ct);
    }

    /// <summary>进行中的各路盯盘（盯盘状态页、测试用）</summary>
    public IReadOnlyList<WatchSession> Sessions => _sessions.Values.Select(v => v.Session).ToList();

    private async Task LeadAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(PlanInterval, timeProvider);
        do
        {
            try
            {
                await TickAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "盯盘计划失败，{Minutes} 分钟后再试", PlanInterval.TotalMinutes);
            }
        }
        while (await timer.WaitForNextTickAsync(ct));
    }

    /// <summary>每 5 秒续期一次；续期失败（锁已被别人拿走或 Redis 不可用）时取消全部盯盘</summary>
    private async Task RenewAsync(IDistributedLockHandle lease, CancellationTokenSource leading)
    {
        try
        {
            while (!leading.IsCancellationRequested)
            {
                await Task.Delay(RenewInterval, timeProvider, leading.Token);
                bool renewed;
                try
                {
                    renewed = await lease.ExtendAsync(LeaseExpiry);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "盯盘租约续期失败");
                    renewed = false;
                }

                if (!renewed)
                {
                    logger.LogWarning("失去盯盘租约，停止全部盯盘");
                    await leading.CancelAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RunSessionAsync(WatchSession session, CancellationToken ct)
    {
        try
        {
            await session.RunAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            // 一路盯盘出错不影响其他各路；运行记录停在运行中，心跳超时后由 RecoverOrphansAsync 记为中断
            logger.LogError(ex, "任务 {TaskId} 在 {ScheduledAt:O} 的盯盘出错", session.Group.TaskId, session.Group.ScheduledAt);
        }
    }

    private async Task FinishMissedAsync(WatchSession session, CancellationToken ct)
    {
        try
        {
            if (await session.BeginAsync(ct))
            {
                await session.EndAsync(new WatchStop(CollectErrorCodes.Interrupted, "盯盘服务中断，窗口已经结束"), ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "收尾任务 {TaskId} 在 {ScheduledAt:O} 的盯盘失败", session.Group.TaskId, session.Group.ScheduledAt);
        }
    }

    /// <summary>
    /// 运行中、心跳超过 5 分钟、又不归任何进行中的盯盘的运行记录（如事件已被人工处理，计划中不再有这一路）记为中断（ERR-COL-009）
    /// </summary>
    private async Task RecoverOrphansAsync(SuanpanDbContext db, DateTime now, CancellationToken ct)
    {
        var active = _sessions.Values.Select(v => v.Session.RunId).Where(id => id > 0).ToList();
        var staleBefore = now - OrphanGrace;
        var recovered = await db.CollectRuns
            .Where(r => r.Status == CollectRunStatus.Running && r.Trigger == CollectRunTrigger.Watch && r.ScheduledFor != null
                && !active.Contains(r.Id) && (r.HeartbeatAt ?? r.StartedAt ?? r.QueuedAt) < staleBefore)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.Status, CollectRunStatus.Failed)
                .SetProperty(r => r.EndedAt, now)
                .SetProperty(r => r.ErrorCode, CollectErrorCodes.Interrupted)
                .SetProperty(r => r.ErrorMessage, "执行中断：盯盘服务中断，这一路没有接着盯"), ct);
        if (recovered > 0)
        {
            logger.LogError("{Count} 路盯盘执行中断，已记为失败（{ErrorCode}）", recovered, CollectErrorCodes.Interrupted);
        }
    }

    private async Task StopSessionsAsync()
    {
        var running = _sessions.Values.Select(v => v.Running).ToList();
        _sessions.Clear();
        try
        {
            await Task.WhenAll(running);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "停止盯盘时有一路出错");
        }
    }
}
