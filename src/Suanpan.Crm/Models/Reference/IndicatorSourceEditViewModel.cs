using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Suanpan.Crm.Models.Reference;

/// <summary>新增或编辑指标与数据源的映射（基础数据设计 §3.8、§6.2）。字段规则由服务校验，属性名与服务的输入一致；编辑时指标只读</summary>
public sealed class IndicatorSourceEditViewModel
{
    [Display(Name = "指标")]
    public string? IndicatorCode { get; set; }

    [Required]
    [Display(Name = "数据源")]
    public long? SourceId { get; set; }

    [Required]
    [StringLength(128)]
    [Display(Name = "外部标识")]
    public string? ExternalId { get; set; }

    [Required]
    [StringLength(64)]
    [Display(Name = "解析器")]
    public string? ParserKey { get; set; }

    [Required]
    [Range(1, short.MaxValue)]
    [Display(Name = "优先级")]
    public short? Priority { get; set; }

    /// <summary>下拉框选项。绑定时为空，操作日志序列化请求参数时跳过空值（前端约定 §5.2）</summary>
    [BindNever]
    public IndicatorSourceFormOptions? Options { get; set; }
}
