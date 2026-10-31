using Suanpan.Core.Calendar.Entities;

namespace Suanpan.Services.Calendar.Watch;

/// <summary>
/// 盯盘对日历事件状态的修改（采集设计 §5.7，FR-003，日历设计 §3.2）：进入盯盘窗口改为"盯盘中"，T+10 分钟无值改为"延迟"，
/// 盯盘期间官方改期退回"待公布"。状态按 CalendarEventStatusRule 检查，不符合的事件跳过（已被入库、人工改过）；
/// 在行锁内修改，与公布值入库、人工操作互斥；写状态历史，操作者为采集，带数据源和盯盘的运行记录
/// </summary>
public interface ICalendarWatchService
{
    /// <summary>待公布 → 盯盘中；返回改了的事件 id（已经是盯盘中的不重复改，也不返回）</summary>
    Task<IReadOnlyList<long>> StartAsync(IReadOnlyCollection<long> eventIds, long sourceId, long runId, CancellationToken ct = default);

    /// <summary>
    /// 盯盘中 → 延迟（ERR-EVT-001）；提交后告警：高重要性为"紧急"，其他为"重要"（BR-129）。返回改了的事件 id
    /// </summary>
    Task<IReadOnlyList<long>> DelayAsync(IReadOnlyCollection<long> eventIds, long sourceId, long runId, CancellationToken ct = default);

    /// <summary>盯盘中 → 待公布：盯盘期间官方改期（PRD v1.0.0.26）。事件已不是盯盘中时返回 false</summary>
    Task<bool> ReturnToScheduledAsync(long eventId, long sourceId, long runId, CancellationToken ct = default);

    /// <summary>这一路事件的当前状态（判断拿到值、改期、取消），不跟踪</summary>
    Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(IReadOnlyCollection<long> eventIds, CancellationToken ct = default);
}
