using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Common.Localization;

namespace Suanpan.Services.Calendar.Events.Models;

/// <summary>
/// 待确认事件列表的一行（日历设计 §9.1）：原排期（UTC、当地时间和时区、精度）、维护排期的数据源、变为待确认的时间；
/// 保留、取消时带上 <see cref="RowVersion"/>（ERR-EVT-009）
/// </summary>
public sealed record UnconfirmedEventRow(
    long Id,
    string IndicatorCode,
    BilingualText IndicatorShortName,
    string ReferencePeriod,
    DateTime? ScheduledAt,
    DateTime? ScheduledLocal,
    string TimeZone,
    TimePrecision TimePrecision,
    long? ScheduleSourceId,
    string? ScheduleSourceName,
    DateTime? UnconfirmedAt,
    int RowVersion);
