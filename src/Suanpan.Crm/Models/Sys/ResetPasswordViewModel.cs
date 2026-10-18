using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Suanpan.Crm.Models.Sys;

/// <summary>管理员重置密码：设一个临时密码线下告知本人，下次登录必须修改</summary>
public sealed class ResetPasswordViewModel
{
    public long Id { get; set; }

    [BindNever]
    public string Username { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "临时密码")]
    public string? NewPassword { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword))]
    [Display(Name = "确认临时密码")]
    public string? ConfirmPassword { get; set; }
}
