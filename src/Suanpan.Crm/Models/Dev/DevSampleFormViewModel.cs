using System.ComponentModel.DataAnnotations;

namespace Suanpan.Crm.Models.Dev;

/// <summary>示例表单：只标 [Display] 中文名，校验提示用全局的中文默认提示</summary>
public sealed class DevSampleFormViewModel
{
    [Required]
    [StringLength(20, MinimumLength = 2)]
    [Display(Name = "名称")]
    public string? Name { get; set; }

    [Range(1, 100)]
    [Display(Name = "数量")]
    public int Quantity { get; set; } = 1;

    [EmailAddress]
    [Display(Name = "通知邮箱")]
    public string? Email { get; set; }

    [StringLength(200)]
    [Display(Name = "备注")]
    public string? Remark { get; set; }

    [Display(Name = "启用")]
    public bool IsEnabled { get; set; } = true;
}
