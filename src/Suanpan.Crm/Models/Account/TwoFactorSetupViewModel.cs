using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Suanpan.Crm.Models.Account;

/// <summary>绑定 TOTP：扫码或手动输入密钥，再输入一次验证码</summary>
public sealed class TwoFactorSetupViewModel
{
    [Required]
    [RegularExpression(@"^\s*\d{6}\s*$", ErrorMessage = "请输入 6 位数字验证码")]
    [Display(Name = "验证码")]
    public string? Code { get; set; }

    /// <summary>加密后的待绑定密钥，放在隐藏域里，验证通过才保存</summary>
    [Required]
    public string? ProtectedSecret { get; set; }

    public string? ReturnUrl { get; set; }

    /// <summary>手动输入用的密钥，每 4 位一组</summary>
    [BindNever]
    public string SecretForDisplay { get; set; } = string.Empty;

    /// <summary>二维码图片（data URI）</summary>
    [BindNever]
    public string QrCodeDataUri { get; set; } = string.Empty;

    /// <summary>已登录后重新绑定；为 false 表示登录过程中的首次绑定</summary>
    [BindNever]
    public bool IsSignedIn { get; set; }
}
