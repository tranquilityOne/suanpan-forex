namespace Suanpan.Services.Calendar.Events.Models;

/// <summary>待确认事件列表的筛选条件（日历设计 §9.1）</summary>
public sealed record UnconfirmedEventQuery(string? IndicatorCode = null, int Page = 1, int PageSize = 20);
