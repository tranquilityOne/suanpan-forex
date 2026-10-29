using Suanpan.Core.Calendar.Rules;
using Suanpan.Core.Reference.Enums;
using Xunit;

namespace Suanpan.Core.Tests.Calendar.Rules;

/// <summary>参考期的写法（日历设计 §3.1）：按发布频率固定格式，不合法的条目入库时按"时间解析失败"处理（ERR-PARSE-005）</summary>
public class ReferencePeriodRuleTests
{
    [Theory]
    [InlineData("2026-09", ReleaseFrequency.Monthly)]
    [InlineData("2026-12", ReleaseFrequency.Monthly)]
    [InlineData("2026-Q3", ReleaseFrequency.Quarterly)]
    [InlineData("2026-10-24", ReleaseFrequency.Weekly)]
    [InlineData("2028-02-29", ReleaseFrequency.Irregular)]
    public void Validate_AcceptsTheFormatOfEachFrequency(string period, ReleaseFrequency frequency)
        => Assert.Null(ReferencePeriodRule.Validate(period, frequency));

    [Theory]
    [InlineData(null, ReleaseFrequency.Monthly)]
    [InlineData("2026-9", ReleaseFrequency.Monthly)]
    [InlineData("2026-13", ReleaseFrequency.Monthly)]
    [InlineData("2026-Q3", ReleaseFrequency.Monthly)]
    [InlineData("2026-Q5", ReleaseFrequency.Quarterly)]
    [InlineData("2026-09", ReleaseFrequency.Quarterly)]
    [InlineData("2026-02-30", ReleaseFrequency.Weekly)]
    [InlineData("2026/10/24", ReleaseFrequency.Weekly)]
    [InlineData("2026-10", ReleaseFrequency.Irregular)]
    public void Validate_RejectsOtherFormats(string? period, ReleaseFrequency frequency)
        => Assert.NotNull(ReferencePeriodRule.Validate(period, frequency));

    /// <summary>同一频率的参考期按字符串排序即按时间排序，跨年、跨季度也成立</summary>
    [Theory]
    [InlineData("2026-09", "2026-10")]
    [InlineData("2025-12", "2026-01")]
    [InlineData("2026-Q4", "2027-Q1")]
    [InlineData("2026-10-24", "2026-10-31")]
    public void Compare_OrdersByTime(string earlier, string later)
    {
        Assert.True(ReferencePeriodRule.Compare(earlier, later) < 0);
        Assert.True(ReferencePeriodRule.Compare(later, earlier) > 0);
    }

    /// <summary>上一期：跨年、跨季度；不定期没有固定间隔、写法不对时返回 null</summary>
    [Theory]
    [InlineData("2026-10", ReleaseFrequency.Monthly, "2026-09")]
    [InlineData("2026-01", ReleaseFrequency.Monthly, "2025-12")]
    [InlineData("2026-Q3", ReleaseFrequency.Quarterly, "2026-Q2")]
    [InlineData("2026-Q1", ReleaseFrequency.Quarterly, "2025-Q4")]
    [InlineData("2026-01-03", ReleaseFrequency.Weekly, "2025-12-27")]
    [InlineData("2026-10-29", ReleaseFrequency.Irregular, null)]
    [InlineData("2026-9", ReleaseFrequency.Monthly, null)]
    public void Previous_StepsBackOnePeriod(string period, ReleaseFrequency frequency, string? expected)
        => Assert.Equal(expected, ReferencePeriodRule.Previous(period, frequency));
}
