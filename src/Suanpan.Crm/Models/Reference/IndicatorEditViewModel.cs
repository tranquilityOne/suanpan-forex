using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Suanpan.Core.Reference.Enums;
using Suanpan.Core.Reference.Rules;

namespace Suanpan.Crm.Models.Reference;

/// <summary>
/// 新增或编辑指标（基础数据设计 §3.4、§6）。编码和所属经济体创建后不可修改；字段规则、编码格式由服务校验。
/// 属性名与服务的输入一致，服务返回的出错字段直接对应到这里。Reason 由操作日志过滤器按属性名采集（BR-031）
/// </summary>
public sealed class IndicatorEditViewModel
{
    [Required]
    [Display(Name = "指标编码")]
    public string? Code { get; set; }

    [Required]
    [Display(Name = "经济体")]
    public string? EconomyCode { get; set; }

    [Required]
    [Display(Name = "货币")]
    public string? CurrencyCode { get; set; }

    [Required]
    [StringLength(64)]
    [Display(Name = "中文名称")]
    public string? NameZh { get; set; }

    [Required]
    [StringLength(128)]
    [Display(Name = "英文名称")]
    public string? NameEn { get; set; }

    [Required]
    [StringLength(32)]
    [Display(Name = "中文简称")]
    public string? ShortNameZh { get; set; }

    [Required]
    [StringLength(64)]
    [Display(Name = "英文简称")]
    public string? ShortNameEn { get; set; }

    [Required]
    [Display(Name = "类别")]
    public IndicatorCategory? Category { get; set; }

    [Required]
    [Display(Name = "数值类型")]
    public IndicatorValueType? ValueType { get; set; }

    [Required]
    [Display(Name = "单位")]
    public IndicatorUnit? Unit { get; set; }

    [Required]
    [Range(0, IndicatorRule.MaxDecimals)]
    [Display(Name = "小数位")]
    public short? Decimals { get; set; }

    [Required]
    [Display(Name = "发布频率")]
    public ReleaseFrequency? Frequency { get; set; }

    [Required]
    [Display(Name = "重要性")]
    public Importance? Importance { get; set; }

    [Required]
    [Display(Name = "影响方向")]
    public ImpactDirection? Direction { get; set; }

    [Required]
    [Display(Name = "持平容差")]
    public decimal? FlatTolerance { get; set; }

    [Display(Name = "合理下限")]
    public decimal? RangeMin { get; set; }

    [Display(Name = "合理上限")]
    public decimal? RangeMax { get; set; }

    [Display(Name = "标准差倍数")]
    public decimal? SigmaN { get; set; }

    [Required]
    [StringLength(64)]
    [Display(Name = "中文发布机构")]
    public string? AgencyZh { get; set; }

    [Required]
    [StringLength(128)]
    [Display(Name = "英文发布机构")]
    public string? AgencyEn { get; set; }

    [Required]
    [Display(Name = "默认发布时间")]
    public TimeOnly? DefaultReleaseTime { get; set; }

    [Display(Name = "中文说明")]
    public string? DescriptionZh { get; set; }

    [Display(Name = "英文说明")]
    public string? DescriptionEn { get; set; }

    /// <summary>修改理由：影响方向改变时必填（BR-031），记入操作日志的原因列</summary>
    [StringLength(256)]
    [Display(Name = "修改理由")]
    public string? Reason { get; set; }

    /// <summary>编辑前的影响方向，页面据此提示"修改理由"何时必填；新增时为空</summary>
    [BindNever]
    public ImpactDirection? OriginalDirection { get; set; }

    /// <summary>
    /// 下拉框选项。绑定时为空，操作日志序列化请求参数时跳过空值，所以选项不会记进日志
    /// </summary>
    [BindNever]
    public IndicatorFormOptions? Options { get; set; }
}
