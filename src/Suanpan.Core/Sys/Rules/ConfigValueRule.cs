using System.Globalization;
using System.Text.Json;
using Suanpan.Core.Sys.Enums;

namespace Suanpan.Core.Sys.Rules;

/// <summary>
/// 系统配置值按类型校验并规范化（sys 设计 §10.2）：数字按 InvariantCulture 解析；
/// 布尔接受 true / false / 1 / 0，统一保存为 true / false；JSON 必须能解析。
/// </summary>
public static class ConfigValueRule
{
    private const NumberStyles DecimalStyle = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    /// <summary>值符合类型时返回 true，并输出规范化后的值</summary>
    public static bool TryNormalize(ConfigValueType valueType, string? value, out string normalized)
    {
        normalized = string.Empty;
        if (value is null)
        {
            return false;
        }

        var trimmed = value.Trim();
        switch (valueType)
        {
            case ConfigValueType.String:
                normalized = value;
                return true;

            case ConfigValueType.Int when int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number):
                normalized = number.ToString(CultureInfo.InvariantCulture);
                return true;

            // 不允许千分位：否则把小数点误写成逗号的 "0,25" 会被当成 25
            case ConfigValueType.Decimal when decimal.TryParse(trimmed, DecimalStyle, CultureInfo.InvariantCulture, out var amount):
                normalized = amount.ToString(CultureInfo.InvariantCulture);
                return true;

            case ConfigValueType.Bool when trimmed.ToLowerInvariant() is "true" or "1":
                normalized = "true";
                return true;

            case ConfigValueType.Bool when trimmed.ToLowerInvariant() is "false" or "0":
                normalized = "false";
                return true;

            case ConfigValueType.Json when IsJson(trimmed):
                normalized = trimmed;
                return true;

            default:
                return false;
        }
    }

    private static bool IsJson(string value)
    {
        try
        {
            using var _ = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
