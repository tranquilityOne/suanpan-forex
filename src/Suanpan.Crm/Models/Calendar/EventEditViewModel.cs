using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Suanpan.Services.Calendar.Events;
using Suanpan.Services.Calendar.Events.Models;

namespace Suanpan.Crm.Models.Calendar;

/// <summary>
/// 修改事件的数值（日历设计 §3.7、§9.3）：打开时预填当前值，只保存改了的字段，改了的字段自动锁定；原因必填（ERR-EVT-008），
/// 并由操作日志过滤器按属性名记入原因列；带读取时的 row_version（ERR-EVT-009）。待复核的事件在这里"改为人工值"
/// </summary>
public sealed class EventEditViewModel
{
    [Display(Name = "公布值")]
    public decimal? Actual { get; set; }

    [Display(Name = "公布值上限")]
    public decimal? ActualUpper { get; set; }

    [Display(Name = "前值")]
    public decimal? Previous { get; set; }

    [Display(Name = "前值上限")]
    public decimal? PreviousUpper { get; set; }

    [Display(Name = "前值修正")]
    public decimal? PreviousRevised { get; set; }

    [Required]
    [StringLength(ICalendarEventService.MaxReasonLength)]
    [Display(Name = "修改原因")]
    public string? Reason { get; set; }

    public int RowVersion { get; set; }

    [BindNever]
    public EventDetail? Detail { get; set; }

    /// <summary>预填当前值，按指标小数位显示</summary>
    public static EventEditViewModel From(EventDetail detail)
    {
        decimal? Round(decimal? value) => EventValueFormat.Round(value, detail.Indicator.Decimals);
        return new EventEditViewModel
        {
            Actual = Round(detail.Event.Actual),
            ActualUpper = Round(detail.Event.ActualUpper),
            Previous = Round(detail.Event.Previous),
            PreviousUpper = Round(detail.Event.PreviousUpper),
            PreviousRevised = Round(detail.Event.PreviousRevised),
            RowVersion = detail.Event.RowVersion,
            Detail = detail,
        };
    }

    public EventValuesInput ToInput() => new(Actual, ActualUpper, Previous, PreviousUpper, PreviousRevised);
}
