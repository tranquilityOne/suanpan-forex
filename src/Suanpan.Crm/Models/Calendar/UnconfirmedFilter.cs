using Suanpan.Services.Calendar.Events.Models;

namespace Suanpan.Crm.Models.Calendar;

/// <summary>待确认事件列表的筛选条件（日历设计 §9.1）</summary>
public sealed class UnconfirmedFilter
{
    public string? IndicatorCode { get; set; }

    public UnconfirmedEventQuery ToQuery(int page = 1) => new(IndicatorCode, page);
}
