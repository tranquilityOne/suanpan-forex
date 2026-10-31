using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Suanpan.Services.Calendar.Events;
using Suanpan.Services.Calendar.Events.Models;

namespace Suanpan.Crm.Models.Calendar.Forecasts;

/// <summary>
/// 公布后更正预期值（日历设计 §9.4，BR-057）：只有管理员，原因必填，写进变更历史、锁定信息，
/// 并由操作日志过滤器按属性名记入原因列
/// </summary>
public sealed class ForecastCorrectViewModel : ForecastFormModel
{
    [Required]
    [StringLength(ICalendarEventService.MaxReasonLength)]
    [Display(Name = "更正原因")]
    public string? Reason { get; set; }

    [BindNever]
    public EventDetail? Detail { get; set; }

    [BindNever]
    public IReadOnlyList<string> NoteOptions { get; set; } = [];

    public static ForecastCorrectViewModel From(EventDetail detail, IReadOnlyList<string> noteOptions) => new()
    {
        Forecast = EventValueFormat.Round(detail.Event.Forecast, detail.Indicator.Decimals),
        ForecastUpper = EventValueFormat.Round(detail.Event.ForecastUpper, detail.Indicator.Decimals),
        Note = detail.Event.ForecastNote,
        RowVersion = detail.Event.RowVersion,
        Detail = detail,
        NoteOptions = noteOptions,
    };
}
