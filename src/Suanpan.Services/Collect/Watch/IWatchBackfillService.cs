using Suanpan.Core.Collect.Rules;

namespace Suanpan.Services.Collect.Watch;

/// <summary>
/// 延迟后的补采（BR-017，采集设计 §5.7）：Collector 的盯盘服务每分钟调用一次。"延迟"的事件按公布值任务合并，
/// 这个任务距上一次运行超过补采间隔、且没有排队或运行中的记录时，创建一条普通运行记录（触发方式"盯盘"）下发执行
/// </summary>
public interface IWatchBackfillService
{
    /// <summary>
    /// 补采间隔按延迟了多久分两段（WatchRule.BackfillInterval），同一任务下取最快的；延迟超过快间隔的时长时，
    /// 每个事件告警一次（重要，BR-129）。返回创建的运行记录 id
    /// </summary>
    Task<IReadOnlyList<long>> RunAsync(BackfillSettings settings, CancellationToken ct = default);
}
