using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Core.Common.Validation;
using Suanpan.Core.Reference.Entities;
using Suanpan.Core.Reference.Enums;

namespace Suanpan.Core.Reference.Rules;

/// <summary>
/// 指标与数据源映射的字段校验（基础数据设计 §3.8，BR-034）。指标、数据源是否存在，解析器是否在目录中，
/// 同一指标的数据源和优先级是否重复，要查库或读目录，由服务校验。
/// </summary>
public static class IndicatorSourceRule
{
    /// <summary>主源的优先级；2 起为备源</summary>
    public const short MainPriority = 1;

    /// <summary>
    /// 外部标识必填；公布值映射的优先级必填且从 1 开始，排期映射不设优先级；盯盘参数只对公布值映射有效，在范围内（BR-013）。
    /// 返回第一条错误，全部通过时返回 null
    /// </summary>
    public static FieldError? Validate(IndicatorSource mapping)
    {
        if (string.IsNullOrWhiteSpace(mapping.ExternalId))
        {
            return new(nameof(IndicatorSource.ExternalId), "请填写外部标识");
        }

        if (mapping.Purpose == IndicatorSourcePurpose.Schedule && (mapping.WatchLeadSeconds is not null || mapping.WatchWindowMinutes is not null))
        {
            return new(nameof(IndicatorSource.WatchLeadSeconds), "排期映射不设盯盘参数，请留空");
        }

        if (mapping.WatchLeadSeconds is < WatchRule.MinLeadSeconds or > WatchRule.MaxLeadSeconds)
        {
            return new(nameof(IndicatorSource.WatchLeadSeconds),
                $"盯盘提前秒数必须在 {WatchRule.MinLeadSeconds} ~ {WatchRule.MaxLeadSeconds} 之间，用默认值时留空");
        }

        if (mapping.WatchWindowMinutes is < WatchRule.MinWindowMinutes or > WatchRule.MaxWindowMinutes)
        {
            return new(nameof(IndicatorSource.WatchWindowMinutes),
                $"盯盘窗口必须在 {WatchRule.MinWindowMinutes} ~ {WatchRule.MaxWindowMinutes} 分钟之间，用默认值时留空");
        }

        return mapping.Purpose switch
        {
            IndicatorSourcePurpose.Release when mapping.Priority is null
                => new(nameof(IndicatorSource.Priority), "公布值映射请填写优先级：1 为主源，2 起为备源"),
            IndicatorSourcePurpose.Release when mapping.Priority < MainPriority
                => new(nameof(IndicatorSource.Priority), "优先级从 1 开始：1 为主源，2 起为备源"),
            IndicatorSourcePurpose.Schedule when mapping.Priority is not null
                => new(nameof(IndicatorSource.Priority), "排期映射不设优先级，请留空"),
            _ => null,
        };
    }

    /// <summary>用途要求的解析器目标数据类型：排期用"日历排期"的解析器，公布值用"公布值"的解析器</summary>
    public static CollectTargetType ParserTargetType(IndicatorSourcePurpose purpose)
        => purpose == IndicatorSourcePurpose.Schedule ? CollectTargetType.Calendar : CollectTargetType.Release;

    /// <summary>页面上的优先级名称：1 为主源，其余为备源 N</summary>
    public static string PriorityName(short priority) => priority == MainPriority ? "主源" : $"备源 {priority - MainPriority}";
}
