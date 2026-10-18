using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Suanpan.Crm.Models.Sys;

/// <summary>编辑账号：姓名、邮箱、角色；登录名不可改</summary>
public sealed class UserEditViewModel
{
    public long Id { get; set; }

    [BindNever]
    [Display(Name = "登录名")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(64)]
    [Display(Name = "姓名")]
    public string? RealName { get; set; }

    [EmailAddress]
    [StringLength(128)]
    [Display(Name = "邮箱")]
    public string? Email { get; set; }

    [Display(Name = "角色")]
    public List<long> RoleIds { get; set; } = [];

    [BindNever]
    public IReadOnlyList<RoleOption> RoleOptions { get; set; } = [];
}
