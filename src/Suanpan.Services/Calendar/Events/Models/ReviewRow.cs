using Suanpan.Core.Calendar.Entities;

namespace Suanpan.Services.Calendar.Events.Models;

/// <summary>
/// 待复核清单的一行（日历设计 §9.3）：事件和指标信息、进入待复核的原因（最近一条状态变更的原因）、各数据源的候选值；
/// SourceNames 为候选值中数据源 id → 名称
/// </summary>
public sealed record ReviewRow(
    EventRow Row,
    string? Reason,
    IReadOnlyList<CalendarEventValue> Candidates,
    IReadOnlyDictionary<long, string> SourceNames);
