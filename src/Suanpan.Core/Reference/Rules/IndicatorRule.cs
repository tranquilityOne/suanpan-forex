using Suanpan.Core.Common.Localization;
using Suanpan.Core.Reference.Entities;
using Suanpan.Core.Reference.Enums;

namespace Suanpan.Core.Reference.Rules;

/// <summary>
/// 指标保存时的字段校验（基础数据设计 §3.4，BR-030、BR-031）。编码格式见 <see cref="ReferenceCodeRule.ValidateIndicatorCode"/>；
/// 编码是否重复、经济体和货币是否存在且启用要查库，由服务校验。
/// </summary>
public static class IndicatorRule
{
    /// <summary>小数位上限：数值列为 numeric(18,6)</summary>
    public const short MaxDecimals = 6;

    /// <summary>SigmaN 为空时，偏离几倍标准差算异常（BR-133）</summary>
    public const decimal DefaultSigmaN = 4m;

    /// <summary>
    /// 新增和修改共用：BR-030 的必填字段齐全（文本不能为空白，枚举必须是已定义的值）、小数位 0 ~ 6、
    /// 持平容差不小于 0、上下限都填时下限不大于上限、标准差倍数填写时大于 0。返回第一条错误，全部通过时返回 null
    /// </summary>
    public static IndicatorRuleError? Validate(Indicator indicator)
    {
        var error = RequiredText(indicator.Name, nameof(Indicator.Name), "名称")
            ?? RequiredText(indicator.ShortName, nameof(Indicator.ShortName), "简称")
            ?? RequiredText(indicator.Agency, nameof(Indicator.Agency), "发布机构")
            ?? RequiredEnum(indicator.Category, nameof(Indicator.Category), "类别")
            ?? RequiredEnum(indicator.ValueType, nameof(Indicator.ValueType), "数值类型")
            ?? RequiredEnum(indicator.Unit, nameof(Indicator.Unit), "单位")
            ?? RequiredEnum(indicator.Frequency, nameof(Indicator.Frequency), "发布频率")
            ?? RequiredEnum(indicator.Importance, nameof(Indicator.Importance), "重要性")
            ?? RequiredEnum(indicator.Direction, nameof(Indicator.Direction), "影响方向");
        if (error is not null)
        {
            return error;
        }

        if (indicator.Decimals is < 0 or > MaxDecimals)
        {
            return new(nameof(Indicator.Decimals), $"小数位必须在 0 ~ {MaxDecimals} 之间");
        }

        if (indicator.FlatTolerance < 0)
        {
            return new(nameof(Indicator.FlatTolerance), "持平容差不能小于 0");
        }

        if (indicator.RangeMin is { } min && indicator.RangeMax is { } max && min > max)
        {
            return new(nameof(Indicator.RangeMin), "合理下限不能大于合理上限");
        }

        if (indicator.SigmaN <= 0)
        {
            return new(nameof(Indicator.SigmaN), "标准差倍数必须大于 0，不填时按 4 倍");
        }

        return null;
    }

    /// <summary>
    /// BR-031：影响方向改变时必须填写理由（理由由操作日志记录，审计日志可查）。
    /// 返回 null 表示可以保存，否则返回面向用户的提示
    /// </summary>
    public static string? ValidateDirectionChange(ImpactDirection current, ImpactDirection updated, string? reason)
        => current != updated && string.IsNullOrWhiteSpace(reason) ? "修改影响方向时必须填写修改理由" : null;

    private static IndicatorRuleError? RequiredText(BilingualText? text, string field, string label)
    {
        if (string.IsNullOrWhiteSpace(text?.Zh))
        {
            return new($"{field}Zh", $"请填写中文{label}");
        }

        return string.IsNullOrWhiteSpace(text.En) ? new($"{field}En", $"请填写英文{label}") : null;
    }

    private static IndicatorRuleError? RequiredEnum<TEnum>(TEnum value, string field, string label)
        where TEnum : struct, Enum
        => Enum.IsDefined(value) ? null : new(field, $"请选择{label}");
}
