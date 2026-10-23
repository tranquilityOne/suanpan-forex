using System.ComponentModel.DataAnnotations;

namespace Suanpan.Crm.Models.Reference;

/// <summary>新增或编辑货币；编码创建后不可修改，格式由服务校验（基础数据设计 §3.1）</summary>
public sealed class CurrencyEditViewModel
{
    [Required]
    [Display(Name = "货币编码")]
    public string? Code { get; set; }

    [Required]
    [StringLength(32)]
    [Display(Name = "中文名称")]
    public string? NameZh { get; set; }

    [Required]
    [StringLength(64)]
    [Display(Name = "英文名称")]
    public string? NameEn { get; set; }
}
