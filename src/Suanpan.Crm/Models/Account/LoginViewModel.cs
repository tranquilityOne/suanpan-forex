using System.ComponentModel.DataAnnotations;

namespace Suanpan.Crm.Models.Account;

public sealed class LoginViewModel
{
    [Required]
    [Display(Name = "登录名")]
    public string? Username { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "密码")]
    public string? Password { get; set; }

    public string? ReturnUrl { get; set; }
}
