using System.ComponentModel.DataAnnotations;

namespace Suanpan.Crm.Models.Sys;

/// <summary>新增或编辑一条后台访问 IP 白名单；格式和重复由服务校验（sys 设计 §5.5）</summary>
public sealed class IpWhitelistEditViewModel
{
    public long Id { get; set; }

    [Required]
    [StringLength(64)]
    [Display(Name = "IP 或网段")]
    public string? IpValue { get; set; }

    [StringLength(128)]
    [Display(Name = "备注")]
    public string? Remark { get; set; }

    [Display(Name = "启用")]
    public bool IsEnabled { get; set; } = true;
}
