using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Suanpan.Core.Calendar.Rules;
using Suanpan.Services.Calendar.Forecasts.Models;

namespace Suanpan.Crm.Models.Calendar.Forecasts;

/// <summary>公布前录入、修改预期值的提交（清单的行内表单、事件详情中的表单），带读取时的 row_version（ERR-FC-003）</summary>
public class ForecastFormModel
{
    [Display(Name = "预期值")]
    public decimal? Forecast { get; set; }

    [Display(Name = "预期值上限")]
    public decimal? ForecastUpper { get; set; }

    [StringLength(ForecastRule.MaxNoteLength)]
    [Display(Name = "来源说明")]
    public string? Note { get; set; }

    public int RowVersion { get; set; }

    public ForecastInput ToInput() => new(Forecast, ForecastUpper, Note);

    /// <summary>模型绑定、DataAnnotations 校验没通过时的第一条错误，在这一行提示</summary>
    public static string FirstError(ModelStateDictionary modelState)
        => modelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault(m => !string.IsNullOrEmpty(m)) ?? "预期值格式不对";
}
