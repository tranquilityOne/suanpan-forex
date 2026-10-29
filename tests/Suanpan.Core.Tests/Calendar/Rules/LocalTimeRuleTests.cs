using System.Globalization;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Calendar.Rules;
using Xunit;

namespace Suanpan.Core.Tests.Calendar.Rules;

/// <summary>
/// 发布机构当地时间换算成 UTC（BR-037，FR-007 验收第 2 条）。2026 年美国 3 月 8 日开始、11 月 1 日结束夏令时；
/// 欧洲 3 月 29 日开始、10 月 25 日结束。两地切换日期不同，中间几周美欧的时差会变
/// </summary>
public class LocalTimeRuleTests
{
    /// <summary>夏令时切换周前后的发布时间都按当时的偏移换算</summary>
    [Theory]
    [InlineData("America/New_York", "2026-03-06 08:30", "2026-03-06 13:30")]  // 切换前一周的周五，EST −5
    [InlineData("America/New_York", "2026-03-08 08:30", "2026-03-08 12:30")]  // 切换当天，已是 EDT −4
    [InlineData("America/New_York", "2026-03-13 08:30", "2026-03-13 12:30")]
    [InlineData("America/New_York", "2026-10-30 08:30", "2026-10-30 12:30")]  // 结束前，EDT −4
    [InlineData("America/New_York", "2026-11-06 08:30", "2026-11-06 13:30")]  // 结束后，EST −5
    [InlineData("Europe/London", "2026-03-27 07:00", "2026-03-27 07:00")]     // 美国已切换、英国还没有，GMT
    [InlineData("Europe/London", "2026-03-30 07:00", "2026-03-30 06:00")]     // BST +1
    [InlineData("Europe/London", "2026-10-23 09:30", "2026-10-23 08:30")]
    [InlineData("Europe/London", "2026-10-26 09:30", "2026-10-26 09:30")]
    [InlineData("Europe/Brussels", "2026-03-27 11:00", "2026-03-27 10:00")]   // CET +1
    [InlineData("Europe/Brussels", "2026-03-30 11:00", "2026-03-30 09:00")]   // CEST +2
    [InlineData("Europe/Brussels", "2026-10-23 11:00", "2026-10-23 09:00")]
    [InlineData("Europe/Brussels", "2026-10-26 11:00", "2026-10-26 10:00")]
    [InlineData("Asia/Tokyo", "2026-03-09 08:50", "2026-03-08 23:50")]        // 没有夏令时；换算后是前一天
    [InlineData("Asia/Shanghai", "2026-07-15 09:30", "2026-07-15 01:30")]
    public void ToUtc_FollowsDaylightSavingTime(string zone, string local, string utc)
        => Assert.Equal(new LocalTimeConversion(Time(utc), LocalTimeResolution.Exact), Convert(zone, local));

    /// <summary>夏令时开始当天不存在的时刻按跳过的时长顺延（按切换前的偏移换算）；跳过区间的两端</summary>
    [Theory]
    [InlineData("America/New_York", "2026-03-08 02:30", "2026-03-08 07:30", LocalTimeResolution.ShiftedForward)]  // 按 03:30 EDT 计
    [InlineData("America/New_York", "2026-03-08 02:00", "2026-03-08 07:00", LocalTimeResolution.ShiftedForward)]  // 02:00 本身已被跳过
    [InlineData("America/New_York", "2026-03-08 01:59", "2026-03-08 06:59", LocalTimeResolution.Exact)]
    [InlineData("America/New_York", "2026-03-08 03:00", "2026-03-08 07:00", LocalTimeResolution.Exact)]
    [InlineData("Europe/Brussels", "2026-03-29 02:30", "2026-03-29 01:30", LocalTimeResolution.ShiftedForward)]   // 按 03:30 CEST 计
    public void ToUtc_SkippedTimeShiftsForward(string zone, string local, string utc, LocalTimeResolution resolution)
        => Assert.Equal(new LocalTimeConversion(Time(utc), resolution), Convert(zone, local));

    /// <summary>夏令时结束当天重复出现的时刻取较早的一次（按夏令时换算）；重复区间的两端</summary>
    [Theory]
    [InlineData("America/New_York", "2026-11-01 01:30", "2026-11-01 05:30", LocalTimeResolution.Ambiguous)]  // 01:30 EDT，不是 06:30 的 EST
    [InlineData("America/New_York", "2026-11-01 01:00", "2026-11-01 05:00", LocalTimeResolution.Ambiguous)]
    [InlineData("America/New_York", "2026-11-01 00:59", "2026-11-01 04:59", LocalTimeResolution.Exact)]
    [InlineData("America/New_York", "2026-11-01 02:00", "2026-11-01 07:00", LocalTimeResolution.Exact)]      // 回拨后只出现一次，EST
    [InlineData("Europe/London", "2026-10-25 01:30", "2026-10-25 00:30", LocalTimeResolution.Ambiguous)]     // 01:30 BST
    public void ToUtc_RepeatedTimeTakesTheEarlierOne(string zone, string local, string utc, LocalTimeResolution resolution)
        => Assert.Equal(new LocalTimeConversion(Time(utc), resolution), Convert(zone, local));

    [Theory]
    [InlineData("Eastern Standard Time")]  // Windows 名称不算
    [InlineData("Mars/Olympus_Mons")]
    public void ToUtc_RejectsNonIanaTimeZone(string zone)
        => Assert.Throws<ArgumentException>(() => Convert(zone, "2026-03-06 08:30"));

    /// <summary>同一个 UTC 时刻在东京已是第二天、在纽约还是前一天</summary>
    [Theory]
    [InlineData("Asia/Tokyo", "2026-11-06 16:00", "2026-11-07")]
    [InlineData("America/New_York", "2026-11-06 03:00", "2026-11-05")]
    [InlineData("Europe/London", "2026-11-06 03:00", "2026-11-06")]
    public void LocalDate_UsesTheAgencyTimeZone(string zone, string utc, string expected)
        => Assert.Equal(DateOnly.Parse(expected, CultureInfo.InvariantCulture), LocalTimeRule.LocalDate(Time(utc), zone));

    [Fact]
    public void ToUtc_ReturnsUtcKind()
        => Assert.Equal(DateTimeKind.Utc, Convert("Asia/Tokyo", "2026-03-09 08:50").Utc.Kind);

    private static LocalTimeConversion Convert(string zone, string local)
    {
        var value = Time(local);
        return LocalTimeRule.ToUtc(DateOnly.FromDateTime(value), TimeOnly.FromDateTime(value), zone);
    }

    private static DateTime Time(string text)
        => DateTime.SpecifyKind(DateTime.ParseExact(text, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), DateTimeKind.Utc);
}
