using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Core.Common.Localization;
using Suanpan.Core.Reference.Enums;

namespace Suanpan.Services.Collect.Watch.Models;

/// <summary>盯盘的系统配置（采集设计 §10）：超出范围的值已换成默认值</summary>
/// <param name="Enabled">总开关 collect:watch_enabled</param>
/// <param name="IncludeMedium">是否包括中重要性</param>
/// <param name="Defaults">默认的提前秒数、窗口分钟、请求间隔</param>
/// <param name="PlanMinutes">计划看未来多少分钟（配置值，还要按最大提前秒数用 WatchRule.PlanMinutes 修正）</param>
/// <param name="Backfill">延迟后的补采间隔</param>
public sealed record WatchSettings(bool Enabled, bool IncludeMedium, WatchDefaults Defaults, int PlanMinutes, BackfillSettings Backfill);

/// <summary>盯盘计划中的一个事件</summary>
public sealed record WatchEventItem(
    long EventId, string IndicatorCode, BilingualText ShortName, Importance Importance, string ReferencePeriod, CalendarEventStatus Status);

/// <summary>
/// 一路盯盘（BR-014）：同一数据源、同一公布值任务、同一排期时间的事件合并为一路，只发一路请求；时间参数见 WatchRule.Merge
/// </summary>
public sealed record WatchGroup(
    long SourceId, string SourceName, long TaskId, string TaskName, DateTime ScheduledAt, WatchTiming Timing, WatchWindow Window,
    IReadOnlyList<WatchEventItem> Events);

/// <summary>不能盯盘的原因（采集设计 §5.7）</summary>
public enum WatchProblemKind : short
{
    /// <summary>指标没有"公布值"映射</summary>
    NoMapping = 1,

    /// <summary>映射的数据源都已停用</summary>
    SourceDisabled = 2,

    /// <summary>数据源没有已启用、解析器与映射相同的"公布值"采集任务</summary>
    NoTask = 3,
}

/// <summary>要盯却不能盯的事件：计划时告警一次，盯盘状态页提前标出</summary>
public sealed record WatchProblem(WatchEventItem Event, DateTime ScheduledAt, WatchProblemKind Kind, string Reason);

/// <summary>一段时间内的盯盘计划：按开始时间排列的各路盯盘，以及不能盯盘的事件</summary>
public sealed record WatchPlan(WatchSettings Settings, IReadOnlyList<WatchGroup> Groups, IReadOnlyList<WatchProblem> Problems);

/// <summary>
/// 一路盯盘的运行记录（盯盘状态页的"正在盯盘""最近结束"）：最近一次请求，以及按最近两次请求的开始时间推算的当前间隔
/// （被限流时会变长，ERR-COL-005；只请求过一次时为空）
/// </summary>
public sealed record WatchRunItem(
    long RunId, long SourceId, string? SourceName, long TaskId, DateTime ScheduledAt, CollectRunStatus Status, DateTime? StartedAt, DateTime? EndedAt,
    int AttemptCount, CollectAttempt? LastAttempt, TimeSpan? Interval, string? ErrorMessage, IReadOnlyList<WatchRunEvent> Events);

/// <summary>一路盯盘中的一个事件；入库时延 = 检测时间 − 排期时间（BR-019），没拿到值时为空</summary>
public sealed record WatchRunEvent(
    long EventId, string IndicatorCode, BilingualText ShortName, string ReferencePeriod, CalendarEventStatus Status, DateTime? DetectedAt,
    TimeSpan? Latency);
