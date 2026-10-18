using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Suanpan.Crm.Models.Sys;

/// <summary>新建账号：初始密码线下告知本人，首次登录须改密码并绑定二次验证</summary>
public sealed class UserCreateViewModel
{
    [Required]
    [StringLength(64)]
    [Display(Name = "登录名")]
    public string? Username { get; set; }

    [Required]
    [StringLength(64)]
    [Display(Name = "姓名")]
    public string? RealName { get; set; }

    [EmailAddress]
    [StringLength(128)]
    [Display(Name = "邮箱")]
    public string? Email { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "初始密码")]
    public string? Password { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password))]
    [Display(Name = "确认初始密码")]
    public string? ConfirmPassword { get; set; }

    [Display(Name = "角色")]
    public List<long> RoleIds { get; set; } = [];

    [BindNever]
    public IReadOnlyList<RoleOption> RoleOptions { get; set; } = [];
}
