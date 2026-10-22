using Suanpan.Core.Reference.Rules;
using Xunit;

namespace Suanpan.Core.Tests.Reference.Rules;

/// <summary>经济体的时区只能是 IANA 名称（BR-027、ERR-REF-003）；开发环境（Windows）和部署环境（Linux）结果一致</summary>
public class TimeZoneRuleTests
{
    /// <summary>预置经济体用到的全部时区（基础数据设计 §2.4）</summary>
    public static TheoryData<string> PresetZones => new()
    {
        "America/New_York", "Europe/Brussels", "Europe/Berlin", "Europe/London", "Asia/Tokyo",
        "Asia/Shanghai", "Europe/Zurich", "America/Toronto", "Australia/Sydney", "Pacific/Auckland",
    };

    [Theory]
    [MemberData(nameof(PresetZones))]
    public void PresetZones_AreValidAndOffered(string zone)
    {
        Assert.True(TimeZoneRule.IsValid(zone));
        Assert.Contains(zone, TimeZoneRule.IanaIds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Eastern Standard Time")]   // Windows 名称
    [InlineData("Mars/Olympus_Mons")]
    public void IsValid_RejectsNonIanaNames(string? zone)
        => Assert.False(TimeZoneRule.IsValid(zone));

    [Fact]
    public void IanaIds_AreSortedAndOnlyContainRegionNames()
    {
        Assert.Equal(TimeZoneRule.IanaIds.Order(StringComparer.Ordinal), TimeZoneRule.IanaIds);
        Assert.DoesNotContain(TimeZoneRule.IanaIds, id => id.StartsWith("Etc/", StringComparison.Ordinal) || !id.Contains('/'));
    }
}
