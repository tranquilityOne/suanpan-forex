using Suanpan.Services.Collect.Watch.Models;

namespace Suanpan.Services.Collect.Watch;

/// <summary>
/// 盯盘计划（采集设计 §5.7，FR-003 主流程第 1 步）：Collector 每分钟按它安排盯盘，后台的盯盘状态页也用它显示接下来的计划。
/// 只读，不改事件状态（状态由日历模块的 ICalendarWatchService 修改）
/// </summary>
public interface IWatchPlanService
{
    /// <summary>盯盘的系统配置；值超出范围时按默认值并记警告日志</summary>
    Task<WatchSettings> GetSettingsAsync(CancellationToken ct = default);

    /// <summary>
    /// 排期时间在 [begin, end] 内、要盯的事件（BR-018：精确到分钟；高重要性，配置打开时包括中；指标启用；状态为待公布或盯盘中），
    /// 按"数据源 + 公布值任务 + 排期时间"分路（BR-014），取优先级最高的启用数据源；不能盯盘的事件带原因另列。
    /// 不看总开关，由调用方决定
    /// </summary>
    Task<WatchPlan> GetPlanAsync(DateTime begin, DateTime end, CancellationToken ct = default);

    /// <summary>不能盯盘的事件告警：每个事件 24 小时内只告警一次（重要，Redis 键 collect:watch:unready:{eventId}）</summary>
    Task AlertUnreadyAsync(IEnumerable<WatchProblem> problems, CancellationToken ct = default);

    /// <summary>正在盯盘的各路（运行中的盯盘运行记录），按排期时间排列</summary>
    Task<IReadOnlyList<WatchRunItem>> GetActiveAsync(CancellationToken ct = default);

    /// <summary>结束时间不早于 since 的盯盘（最多 50 路，最近结束的在前），带各事件的入库时延（BR-019）</summary>
    Task<IReadOnlyList<WatchRunItem>> GetRecentAsync(DateTime since, CancellationToken ct = default);
}
