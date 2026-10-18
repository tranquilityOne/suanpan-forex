using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Suanpan.Crm.Models.Sys;

/// <summary>修改权限点：只能改图标、排序、启停；名称、层级、路由以代码为准（sys 设计 §6.3）</summary>
public sealed class PermissionEditViewModel
{
    public long Id { get; set; }

    [BindNever]
    public string PermCode { get; set; } = string.Empty;

    [BindNever]
    public string PermName { get; set; } = string.Empty;

    [StringLength(64)]
    [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "图标名只能用小写字母、数字和连字符，如 users")]
    [Display(Name = "图标")]
    public string? Icon { get; set; }

    [Range(0, 9999)]
    [Display(Name = "排序")]
    public int Sort { get; set; }

    [Display(Name = "启用")]
    public bool IsEnabled { get; set; }
}
