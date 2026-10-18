using System.ComponentModel.DataAnnotations;

namespace Suanpan.Crm.Models.Account;

public sealed class TwoFactorViewModel
{
    [Required]
    [RegularExpression(@"^\s*\d{6}\s*$", ErrorMessage = "请输入 6 位数字验证码")]
    [Display(Name = "验证码")]
    public string? Code { get; set; }

    public string? ReturnUrl { get; set; }
}
