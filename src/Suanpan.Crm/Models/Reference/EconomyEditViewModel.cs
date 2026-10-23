using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Suanpan.Crm.Models.Reference;

/// <summary>新增或编辑经济体；编码创建后不可修改，格式、货币和时区由服务校验（基础数据设计 §3.1–3.3）</summary>
public sealed class EconomyEditViewModel
{
    [Required]
    [Display(Name = "经济体编码")]
    public string? Code { get; set; }

    [Required]
    [StringLength(32)]
    [Display(Name = "中文名称")]
    public string? NameZh { get; set; }

    [Required]
    [StringLength(64)]
    [Display(Name = "英文名称")]
    public string? NameEn { get; set; }

    [Required]
    [Display(Name = "货币")]
    public string? CurrencyCode { get; set; }

    [Required]
    [Display(Name = "时区")]
    public string? TimeZone { get; set; }

    [Display(Name = "显示顺序")]
    public short Sort { get; set; }

    /// <summary>启用的货币；编辑时另加经济体当前使用的货币（即使已停用）</summary>
    [BindNever]
    public IReadOnlyList<SelectListItem> CurrencyOptions { get; set; } = [];

    /// <summary>IANA 时区，按大区分组</summary>
    [BindNever]
    public IReadOnlyList<SelectListItem> TimeZoneOptions { get; set; } = [];
}
