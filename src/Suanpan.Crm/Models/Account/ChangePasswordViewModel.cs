using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Suanpan.Crm.Models.Account;

public sealed class ChangePasswordViewModel
{
    /// <summary>已登录后主动修改时必填；登录过程中强制修改时不显示</summary>
    [DataType(DataType.Password)]
    [Display(Name = "当前密码")]
    public string? CurrentPassword { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "新密码")]
    public string? NewPassword { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword))]
    [Display(Name = "确认新密码")]
    public string? ConfirmPassword { get; set; }

    public string? ReturnUrl { get; set; }

    /// <summary>已登录后主动修改；为 false 表示登录过程中强制修改（首次登录或密码被重置）</summary>
    [BindNever]
    public bool IsSignedIn { get; set; }
}
