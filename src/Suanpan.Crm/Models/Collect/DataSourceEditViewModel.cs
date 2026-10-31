using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;

namespace Suanpan.Crm.Models.Collect;

/// <summary>
/// 新增或编辑数据源（采集设计 §3.1、§8.1）。字段规则由服务校验，属性名与服务的输入一致。
/// 凭据只用来输入新值：留空表示不变，从不回显；属性名含 Credential，操作日志自动脱敏（BR-004、FR-001 验收第 5 条）
/// </summary>
public sealed class DataSourceEditViewModel
{
    [Required]
    [StringLength(64)]
    [Display(Name = "名称")]
    public string? Name { get; set; }

    [Required]
    [StringLength(128)]
    [Display(Name = "所属机构")]
    public string? Agency { get; set; }

    [Display(Name = "经济体")]
    public string? EconomyCode { get; set; }

    [Required]
    [Display(Name = "类型")]
    public DataSourceType? Type { get; set; }

    [Required]
    [StringLength(512)]
    [Display(Name = "基础地址")]
    public string? BaseUrl { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    [Display(Name = "请求频率上限")]
    public int? RateLimit { get; set; }

    [Required]
    [Display(Name = "频率单位")]
    public RateLimitUnit? RateLimitUnit { get; set; }

    [Required]
    [Range(DataSourceRule.MinTimeoutSeconds, DataSourceRule.MaxTimeoutSeconds)]
    [Display(Name = "请求超时（秒）")]
    public short? TimeoutSeconds { get; set; }

    [Range(1, int.MaxValue)]
    [Display(Name = "每日请求上限")]
    public int? DailyLimit { get; set; }

    [Range(WatchRule.MinIntervalSeconds, WatchRule.MaxIntervalSeconds)]
    [Display(Name = "盯盘请求间隔（秒）")]
    public short? WatchIntervalSeconds { get; set; }

    [Display(Name = "需要凭据")]
    public bool CredentialRequired { get; set; }

    [StringLength(1024)]
    [DataType(DataType.Password)]
    [Display(Name = "凭据")]
    public string? Credential { get; set; }

    [Display(Name = "清除凭据")]
    public bool ClearCredential { get; set; }

    [Required]
    [Display(Name = "合规类别")]
    public ComplianceClass? ComplianceClass { get; set; }

    [Required]
    [StringLength(512)]
    [Display(Name = "条款链接")]
    public string? TermsUrl { get; set; }

    [Required]
    [Display(Name = "robots.txt 检查结果")]
    public RobotsResult? RobotsResult { get; set; }

    [StringLength(500)]
    [Display(Name = "备注")]
    public string? Remark { get; set; }

    /// <summary>下拉框选项。绑定时为空，操作日志序列化请求参数时跳过空值（前端约定 §5.2）</summary>
    [BindNever]
    public DataSourceFormOptions? Options { get; set; }
}
