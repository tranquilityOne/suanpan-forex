using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Common.Localization;
using Suanpan.Core.Reference.Enums;

namespace Suanpan.Services.Calendar.Events.Models;

/// <summary>事件列表的一行（日历设计 §9.3）：事件本身，加上显示数值用的指标信息</summary>
public sealed record EventRow(
    CalendarEvent Event,
    BilingualText ShortName,
    string EconomyCode,
    Importance Importance,
    IndicatorValueType ValueType,
    IndicatorUnit Unit,
    short Decimals);
