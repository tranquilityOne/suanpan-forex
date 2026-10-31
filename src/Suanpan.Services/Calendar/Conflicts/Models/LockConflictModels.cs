using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Common.Localization;
using Suanpan.Core.Reference.Enums;

namespace Suanpan.Services.Calendar.Conflicts.Models;

/// <summary>锁定冲突列表（日历设计 §9.5）：待处理 / 已处理两个页签</summary>
public sealed record LockConflictQuery(bool Resolved = false, int Page = 1, int PageSize = 20);

/// <summary>
/// 冲突列表的一行：冲突、所属事件（锁定信息、row_version），显示数值用的指标简称、值类型、单位、小数位，
/// 采集值来自的数据源名称（已删除时为空）
/// </summary>
public sealed record LockConflictRow(
    LockConflict Conflict, CalendarEvent Event, BilingualText ShortName, IndicatorValueType ValueType, IndicatorUnit Unit, short Decimals, string? SourceName);
