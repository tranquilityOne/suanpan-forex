using System.ComponentModel.DataAnnotations;

namespace Suanpan.Crm.Models.Sys;

/// <summary>新建或编辑角色；角色编码创建后不可修改</summary>
public sealed class RoleEditViewModel
{
    public long Id { get; set; }

    [Required]
    [StringLength(64)]
    [RegularExpression("^[a-z][a-z0-9_]{1,63}$", ErrorMessage = "角色编码只能用小写字母、数字和下划线，以字母开头")]
    [Display(Name = "角色编码")]
    public string? RoleCode { get; set; }

    [Required]
    [StringLength(64)]
    [Display(Name = "角色名称")]
    public string? RoleName { get; set; }

    [Display(Name = "启用")]
    public bool IsEnabled { get; set; } = true;

    [StringLength(256)]
    [Display(Name = "备注")]
    public string? Remark { get; set; }
}
