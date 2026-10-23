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
}
