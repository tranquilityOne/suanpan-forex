using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Suanpan.Services.Calendar.Events;
using Suanpan.Services.Calendar.Events.Models;

namespace Suanpan.Crm.Models.Calendar;

/// <summary>
/// 取消待确认事件（日历设计 §9.1）：原因必填（ERR-EVT-008），写进变更历史，并由操作日志过滤器按属性名记入原因列；
/// 带读取时的 row_version（ERR-EVT-009）
/// </summary>
public sealed class UnconfirmedCancelViewModel
{
    [Required]
    [StringLength(ICalendarEventService.MaxReasonLength)]
    [Display(Name = "取消原因")]
    public string? Reason { get; set; }

    public int RowVersion { get; set; }

    /// <summary>页面上显示的事件信息</summary>
    [BindNever]
    public UnconfirmedEventRow? Event { get; set; }
}
