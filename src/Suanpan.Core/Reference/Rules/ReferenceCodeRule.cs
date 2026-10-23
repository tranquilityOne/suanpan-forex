using System.Text.RegularExpressions;

namespace Suanpan.Core.Reference.Rules;

/// <summary>
/// 基础数据的编码（基础数据设计 §3.1，BR-026、BR-029）：货币用 ISO 4217，经济体用 ISO 3166-1 alpha-2（欧元区为 EA），
/// 指标为 {经济体}.{简称}。只校验格式，不内置 ISO 清单。编码创建后不可修改：其他模块按编码引用。
/// </summary>
public static partial class ReferenceCodeRule
{
    /// <summary>指标编码的最大长度，与 ref_indicator.code 的列长一致</summary>
    public const int MaxIndicatorCodeLength = 32;

    /// <summary>去掉首尾空白并转为大写，校验之前先调用</summary>
    public static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>返回 null 表示格式正确，否则返回面向用户的提示</summary>
    public static string? ValidateCurrencyCode(string? code)
        => code is not null && CurrencyCode().IsMatch(code) ? null : "货币编码必须是 3 个大写英文字母（ISO 4217），如 USD";

    /// <summary>返回 null 表示格式正确，否则返回面向用户的提示</summary>
    public static string? ValidateEconomyCode(string? code)
        => code is not null && EconomyCode().IsMatch(code) ? null : "经济体编码必须是 2 个大写英文字母（ISO 3166-1），欧元区为 EA";

    /// <summary>
    /// 指标编码（BR-029）：{经济体}.{简称}，简称由大写字母和数字组成，可以有多段，如 US.CPI.YOY；
    /// 总长不超过 32，必须以所属经济体的编码开头。返回 null 表示正确，否则返回面向用户的提示
    /// </summary>
    public static string? ValidateIndicatorCode(string? code, string economyCode)
    {
        if (code is null || !IndicatorCode().IsMatch(code) || code.Length > MaxIndicatorCodeLength)
        {
            return $"指标编码的格式为 经济体.简称，简称只用大写字母和数字，可以有多段，如 US.CPI.YOY；总长不超过 {MaxIndicatorCodeLength}";
        }

        return code.StartsWith(economyCode + ".", StringComparison.Ordinal) ? null : $"指标编码必须以所属经济体的编码开头，如 {economyCode}.CPI";
    }

    /// <summary>
    /// 前台指标详情页地址中的标识（web-portal BR-015：地址稳定）：转小写，. 换成 -，如 US.NFP → us-nfp。
    /// 编码创建后不可修改，所以地址不会变
    /// </summary>
    public static string ToSlug(string indicatorCode) => indicatorCode.ToLowerInvariant().Replace('.', '-');

    // \z 而不是 $：$ 允许末尾带一个换行
    [GeneratedRegex(@"^[A-Z]{3}\z")]
    private static partial Regex CurrencyCode();

    [GeneratedRegex(@"^[A-Z]{2}\z")]
    private static partial Regex EconomyCode();

    [GeneratedRegex(@"^[A-Z]{2}(\.[A-Z0-9]+)+\z")]
    private static partial Regex IndicatorCode();
}
