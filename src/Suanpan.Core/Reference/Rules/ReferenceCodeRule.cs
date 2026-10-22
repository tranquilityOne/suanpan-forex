using System.Text.RegularExpressions;

namespace Suanpan.Core.Reference.Rules;

/// <summary>
/// 基础数据的编码（基础数据设计 §3.1，BR-026）：货币用 ISO 4217，经济体用 ISO 3166-1 alpha-2（欧元区为 EA）。
/// 只校验格式，不内置 ISO 清单。编码创建后不可修改：其他模块按编码引用。
/// </summary>
public static partial class ReferenceCodeRule
{
    /// <summary>去掉首尾空白并转为大写，校验之前先调用</summary>
    public static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>返回 null 表示格式正确，否则返回面向用户的提示</summary>
    public static string? ValidateCurrencyCode(string? code)
        => code is not null && CurrencyCode().IsMatch(code) ? null : "货币编码必须是 3 个大写英文字母（ISO 4217），如 USD";

    /// <summary>返回 null 表示格式正确，否则返回面向用户的提示</summary>
    public static string? ValidateEconomyCode(string? code)
        => code is not null && EconomyCode().IsMatch(code) ? null : "经济体编码必须是 2 个大写英文字母（ISO 3166-1），欧元区为 EA";

    // \z 而不是 $：$ 允许末尾带一个换行
    [GeneratedRegex(@"^[A-Z]{3}\z")]
    private static partial Regex CurrencyCode();

    [GeneratedRegex(@"^[A-Z]{2}\z")]
    private static partial Regex EconomyCode();
}
