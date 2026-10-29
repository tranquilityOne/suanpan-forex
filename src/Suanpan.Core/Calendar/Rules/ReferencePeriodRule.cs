using System.Globalization;
using System.Text.RegularExpressions;
using Suanpan.Core.Reference.Enums;

namespace Suanpan.Core.Calendar.Rules;

/// <summary>
/// 参考期的写法（日历设计 §3.1）：按指标的发布频率写成固定格式，同一频率的参考期按字符串排序即按时间排序。
/// <list type="bullet">
/// <item>月：YYYY-MM，如 2026-09</item>
/// <item>季：YYYY-Qn，如 2026-Q3</item>
/// <item>周：周结束日 YYYY-MM-DD，如 2026-10-24（初请失业金"截至 10 月 24 日的一周"）</item>
/// <item>不定期：当期日期 YYYY-MM-DD，如议息会议的决议日</item>
/// </list>
/// 解析器输出的参考期在入库时用它校验，不合法的条目按"时间解析失败"处理（ERR-PARSE-005）
/// </summary>
public static partial class ReferencePeriodRule
{
    /// <summary>参考期写法正确时返回 null，否则返回原因</summary>
    public static string? Validate(string? period, ReleaseFrequency frequency)
    {
        if (string.IsNullOrWhiteSpace(period))
        {
            return "缺少参考期";
        }

        var valid = frequency switch
        {
            ReleaseFrequency.Monthly => MonthPattern().IsMatch(period),
            ReleaseFrequency.Quarterly => QuarterPattern().IsMatch(period),
            ReleaseFrequency.Weekly or ReleaseFrequency.Irregular
                => DateOnly.TryParseExact(period, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            _ => false,
        };
        return valid ? null : $"参考期 {period} 的写法不对，应写成{Example(frequency)}";
    }

    /// <summary>比较同一频率的两个参考期：小于 0 表示 a 在前</summary>
    public static int Compare(string a, string b) => string.CompareOrdinal(a, b);

    private static string Example(ReleaseFrequency frequency) => frequency switch
    {
        ReleaseFrequency.Monthly => " YYYY-MM（如 2026-09）",
        ReleaseFrequency.Quarterly => " YYYY-Qn（如 2026-Q3）",
        ReleaseFrequency.Weekly => "周结束日 YYYY-MM-DD（如 2026-10-24）",
        _ => "当期日期 YYYY-MM-DD（如 2026-10-29）",
    };

    [GeneratedRegex("^[0-9]{4}-(0[1-9]|1[0-2])$")]
    private static partial Regex MonthPattern();

    [GeneratedRegex("^[0-9]{4}-Q[1-4]$")]
    private static partial Regex QuarterPattern();
}
