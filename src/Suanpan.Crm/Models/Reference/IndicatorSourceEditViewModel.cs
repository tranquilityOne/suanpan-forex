using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Suanpan.Core.Collect.Rules;
using Suanpan.Core.Reference.Enums;

namespace Suanpan.Crm.Models.Reference;

/// <summary>
/// 新增或编辑指标与数据源的映射（基础数据设计 §3.8、§6.2）。字段规则由服务校验，属性名与服务的输入一致；编辑时指标和用途只读。
/// 优先级、盯盘参数只在公布值映射时填写，排期映射留空（由服务按用途校验）
/// </summary>
public sealed class IndicatorSourceEditViewModel
{
    [Display(Name = "指标")]
    public string? IndicatorCode { get; set; }

    [Required]
    [Display(Name = "数据源")]
    public long? SourceId { get; set; }

    [Required]
    [Display(Name = "用途")]
    public IndicatorSourcePurpose? Purpose { get; set; }

    [Required]
    [StringLength(128)]
    [Display(Name = "外部标识")]
    public string? ExternalId { get; set; }

    [Required]
    [StringLength(64)]
    [Display(Name = "解析器")]
    public string? ParserKey { get; set; }

    [Range(1, short.MaxValue)]
    [Display(Name = "优先级")]
    public short? Priority { get; set; }

    [Range(WatchRule.MinLeadSeconds, WatchRule.MaxLeadSeconds)]
    [Display(Name = "盯盘提前秒数")]
    public short? WatchLeadSeconds { get; set; }

    [Range(WatchRule.MinWindowMinutes, WatchRule.MaxWindowMinutes)]
    [Display(Name = "盯盘窗口（分钟）")]
    public short? WatchWindowMinutes { get; set; }

    /// <summary>下拉框选项。绑定时为空，操作日志序列化请求参数时跳过空值（前端约定 §5.2）</summary>
    [BindNever]
    public IndicatorSourceFormOptions? Options { get; set; }
}
