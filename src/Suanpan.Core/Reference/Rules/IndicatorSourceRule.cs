using Suanpan.Core.Common.Validation;
using Suanpan.Core.Reference.Entities;

namespace Suanpan.Core.Reference.Rules;

/// <summary>
/// 指标与数据源映射的字段校验（基础数据设计 §3.8，BR-034）。指标、数据源是否存在，解析器是否在目录中，
/// 同一指标的数据源和优先级是否重复，要查库或读目录，由服务校验。
/// </summary>
public static class IndicatorSourceRule
{
    /// <summary>主源的优先级；2 起为备源</summary>
    public const short MainPriority = 1;

    /// <summary>外部标识必填；优先级从 1 开始。返回第一条错误，全部通过时返回 null</summary>
    public static FieldError? Validate(IndicatorSource mapping)
    {
        if (string.IsNullOrWhiteSpace(mapping.ExternalId))
        {
            return new(nameof(IndicatorSource.ExternalId), "请填写外部标识");
        }

        return mapping.Priority < MainPriority
            ? new(nameof(IndicatorSource.Priority), "优先级从 1 开始：1 为主源，2 起为备源")
            : null;
    }

    /// <summary>页面上的优先级名称：1 为主源，其余为备源 N</summary>
    public static string PriorityName(short priority) => priority == MainPriority ? "主源" : $"备源 {priority - MainPriority}";
}
