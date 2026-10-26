using Suanpan.Services.Collect.Tasks;
using Xunit;

namespace Suanpan.Services.Tests.Collect;

/// <summary>采集任务的 cron（采集设计 §3.2、§4.2）：5 段写法，按 UTC 解释；不连数据库</summary>
public class CronScheduleTests
{
    private static readonly DateTime Noon = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("*/5 * * * *")]
    [InlineData("0 1 * * *")]
    [InlineData(" 30 8 * * 1-5 ")]
    public void Validate_AcceptsFiveFields(string cron)
        => Assert.Null(CronSchedule.Validate(cron));

    /// <summary>BR-008、ERR-COL-001：带秒的 6 段写法（间隔可能小于 1 分钟）和无法解析的写法都拒绝</summary>
    [Theory]
    [InlineData("")]
    [InlineData("*/10 * * * * *")]
    [InlineData("0 25 * * *")]
    [InlineData("every minute")]
    public void Validate_RejectsOtherForms(string cron)
        => Assert.NotNull(CronSchedule.Validate(cron));

    [Theory]
    [InlineData("  */5   *\t* * *  ", "*/5 * * * *")]
    [InlineData("   ", null)]
    public void Normalize_CollapsesWhitespace(string cron, string? expected)
        => Assert.Equal(expected, CronSchedule.Normalize(cron));

    /// <summary>区间内有多个时间点时只取最近一次：Job 停机后恢复，不逐个补发</summary>
    [Fact]
    public void LatestOccurrence_ReturnsTheLastPointInRange()
        => Assert.Equal(Noon.AddMinutes(10), CronSchedule.LatestOccurrence("*/5 * * * *", Noon.AddMinutes(-12), Noon.AddMinutes(11)));

    /// <summary>起点不含、终点含：同一个时间点不会在相邻两次分发中各算一次</summary>
    [Fact]
    public void LatestOccurrence_ExcludesStartIncludesEnd()
    {
        Assert.Null(CronSchedule.LatestOccurrence("0 12 * * *", Noon, Noon.AddMinutes(1)));
        Assert.Equal(Noon, CronSchedule.LatestOccurrence("0 12 * * *", Noon.AddMinutes(-1), Noon));
    }

    [Fact]
    public void NextOccurrences_AreInUtcOrder()
        => Assert.Equal(
            [Noon.AddHours(13), Noon.AddHours(37), Noon.AddHours(61)],
            CronSchedule.NextOccurrences("0 1 * * *", Noon, 3));
}
