using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Suanpan.Core.Sys.Enums;

namespace Suanpan.Crm.Models.Sys;

/// <summary>修改一个配置项：值按类型校验（sys 设计 §10.2）</summary>
public sealed class ConfigEditViewModel
{
    [Required]
    public string Key { get; set; } = string.Empty;

    [Display(Name = "值")]
    public string? Value { get; set; }

    [BindNever]
    public ConfigValueType ValueType { get; set; }

    [BindNever]
    public string? Description { get; set; }

    [BindNever]
    public string? DefaultValue { get; set; }

    /// <summary>库里有值（不是在用默认值）</summary>
    [BindNever]
    public bool IsStored { get; set; }
}
