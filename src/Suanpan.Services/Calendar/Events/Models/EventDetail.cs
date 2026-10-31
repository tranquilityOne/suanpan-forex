using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Reference.Entities;

namespace Suanpan.Services.Calendar.Events.Models;

/// <summary>
/// 事件详情（FR-009 主流程第 2 步，日历设计 §9.3）：事件和指标，各数据源的数值，锁定冲突（待处理的在前），
/// 变更历史（时间倒序，含改期）；SourceNames 为这些记录中出现的数据源 id → 名称，数据源已删除的不在其中
/// </summary>
public sealed record EventDetail(
    CalendarEvent Event,
    Indicator Indicator,
    IReadOnlyList<CalendarEventValue> Values,
    IReadOnlyList<LockConflict> Conflicts,
    IReadOnlyList<CalendarEventChange> Changes,
    IReadOnlyDictionary<long, string> SourceNames);
