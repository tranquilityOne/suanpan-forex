using Suanpan.Crm.Mvc;
using Xunit;

namespace Suanpan.Crm.Tests.Mvc;

public class DateRangeFilterTests
{
    private static readonly TimeZoneInfo Shanghai = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");

    /// <summary>北京时间 10-17 到 10-18（含两端）= UTC 10-16 16:00 到 10-18 16:00（不含）</summary>
    [Fact]
    public void OperatorDates_BecomeUtcRange_IncludingTheWholeEndDay()
    {
        var (begin, end) = DateRangeFilter.ToUtc(new DateOnly(2026, 10, 17), new DateOnly(2026, 10, 18), Shanghai);

        Assert.Equal(new DateTime(2026, 10, 16, 16, 0, 0, DateTimeKind.Utc), begin);
        Assert.Equal(new DateTime(2026, 10, 18, 16, 0, 0, DateTimeKind.Utc), end);
    }

    [Fact]
    public void MissingDates_MeanNoLimit()
        => Assert.Equal((null, null), DateRangeFilter.ToUtc(null, null, Shanghai));
}
