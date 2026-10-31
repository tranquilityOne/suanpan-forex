using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Reference.Enums;

namespace Suanpan.Services.Calendar.Events.Models;

/// <summary>
/// 事件列表的筛选条件（BR-048，日历设计 §9.3）：排期时间范围（UTC，Begin 含、End 不含；没有排期时间的事件不在范围内）、
/// 指标所属经济体、重要性、状态、只看缺少预期值的
/// </summary>
public sealed record EventQuery(
    DateTime? Begin = null,
    DateTime? End = null,
    string? EconomyCode = null,
    Importance? Importance = null,
    CalendarEventStatus? Status = null,
    bool MissingForecast = false,
    int Page = 1,
    int PageSize = 50);
