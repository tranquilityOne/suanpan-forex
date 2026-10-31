using Suanpan.Core.Calendar.Enums;

namespace Suanpan.Crm.Models.Calendar;

/// <summary>排期的显示（`_EventSchedule` 部分视图）：事件列表、详情和待确认事件共用</summary>
/// <param name="ScheduledAt">UTC，按操作者时区显示</param>
/// <param name="ScheduledLocal">官方的当地时间；仅日期时时间部分为 00:00，待定且没有日期时为空</param>
public sealed record EventScheduleView(DateTime? ScheduledAt, DateTime? ScheduledLocal, string TimeZone, TimePrecision Precision);
