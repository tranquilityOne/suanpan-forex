using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;

namespace Suanpan.Core.Collect.Rules;

/// <summary>
/// 运行记录的状态流转（采集设计 §3.3）：排队 → 运行中 → 成功 / 部分成功 / 失败 / 跳过；排队时也可能直接跳过或失败；
/// 运行中等待重试时状态不变。结束的状态不再变化。改状态时用条件更新（后端约定 §6.4）
/// </summary>
public static class CollectRunStatusRule
{
    public static bool CanTransition(CollectRunStatus from, CollectRunStatus to) => (from, to) switch
    {
        (CollectRunStatus.Queued, CollectRunStatus.Running or CollectRunStatus.Skipped or CollectRunStatus.Failed) => true,
        (CollectRunStatus.Running, CollectRunStatus.Running or CollectRunStatus.Succeeded or CollectRunStatus.PartiallySucceeded
            or CollectRunStatus.Failed or CollectRunStatus.Skipped) => true,
        _ => false,
    };

    /// <summary>已结束：成功、部分成功、失败、跳过</summary>
    public static bool IsFinished(CollectRunStatus status)
        => status is CollectRunStatus.Succeeded or CollectRunStatus.PartiallySucceeded or CollectRunStatus.Failed or CollectRunStatus.Skipped;

    /// <summary>执行中断的判断留出的余量（采集设计 §4.3）</summary>
    public static readonly TimeSpan InterruptGrace = TimeSpan.FromMinutes(5);

    /// <summary>
    /// "运行中"的记录是否已经执行中断（ERR-COL-009，采集设计 §4.3）：等待重试的，计划的重试时间已过去 5 分钟以上（重试消息丢失）；
    /// 不在等待重试的，最近一次请求开始已超过"请求超时 + 5 分钟"（执行到一半进程退出）。每次请求开始时就写入请求记录，
    /// 所以最近一条请求记录的开始时间就是当前这次的。重新解析不发请求，按心跳判断：心跳（没有时取开始时间）超过 5 分钟没有更新（§5.6）
    /// </summary>
    public static bool IsInterrupted(CollectRun run, TimeSpan requestTimeout, DateTime now)
    {
        if (run.Status != CollectRunStatus.Running)
        {
            return false;
        }

        if (run.Trigger == CollectRunTrigger.Reparse)
        {
            return now - (run.HeartbeatAt ?? run.StartedAt ?? run.QueuedAt) > InterruptGrace;
        }

        if (run.NextAttemptAt is { } nextAttemptAt)
        {
            return now - nextAttemptAt > InterruptGrace;
        }

        var lastStartedAt = run.Attempts.Count > 0 ? run.Attempts.Max(a => a.StartedAt) : run.StartedAt ?? run.QueuedAt;
        return now - lastStartedAt > requestTimeout + InterruptGrace;
    }
}
