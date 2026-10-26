using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Suanpan.Crm.Models.Collect;

/// <summary>
/// 新增或编辑采集任务（采集设计 §3.2、§8.1）。字段规则由服务校验，属性名与服务的输入一致；
/// 目标数据类型由解析器决定，不在表单中填写（BR-012）
/// </summary>
public sealed class CollectTaskEditViewModel
{
    [Required]
    [StringLength(64)]
    [Display(Name = "名称")]
    public string? Name { get; set; }

    [Required]
    [Display(Name = "数据源")]
    public long? SourceId { get; set; }

    [Required]
    [StringLength(64)]
    [Display(Name = "解析器")]
    public string? ParserKey { get; set; }

    [StringLength(64)]
    [Display(Name = "cron（UTC）")]
    public string? Cron { get; set; }

    [Display(Name = "解析器参数")]
    public string? Params { get; set; }

    [StringLength(500)]
    [Display(Name = "备注")]
    public string? Remark { get; set; }

    /// <summary>下拉框选项和 cron 预览。绑定时为空，操作日志序列化请求参数时跳过空值（前端约定 §5.2）</summary>
    [BindNever]
    public CollectTaskFormOptions? Options { get; set; }
}
