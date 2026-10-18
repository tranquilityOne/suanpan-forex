using System.Net;
using Suanpan.Services.Sys.Security;
using Xunit;

namespace Suanpan.Services.Tests.Sys.Security;

public class IpWhitelistMatcherTests
{
    [Theory]
    [InlineData("203.0.113.10")]
    [InlineData(" 203.0.113.10 ")]
    [InlineData("203.0.113.0/24")]
    [InlineData("0.0.0.0/0")]
    [InlineData("2001:db8::1")]
    [InlineData("2001:db8::/32")]
    public void Validate_AcceptsSingleIpAndCidr(string value)
    {
        Assert.Null(IpWhitelistMatcher.Validate(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-ip")]
    [InlineData("10")]                       // IPAddress.TryParse 会解释成 0.0.0.10
    [InlineData("10.1")]                     // 会解释成 10.0.0.1
    [InlineData("203.0.113.300")]
    [InlineData("203.0.113.10-203.0.113.50")] // 不支持起止段
    [InlineData("203.0.113.10/24")]           // 主机位不为 0
    [InlineData("203.0.113.0/33")]
    [InlineData("10.0/16")]
    public void Validate_RejectsInvalidValues(string value)
    {
        Assert.NotNull(IpWhitelistMatcher.Validate(value));
    }

    [Theory]
    [InlineData("203.0.113.10", "203.0.113.10", true)]
    [InlineData("203.0.113.10", "203.0.113.11", false)]
    [InlineData("203.0.113.0/24", "203.0.113.255", true)]
    [InlineData("203.0.113.0/24", "203.0.114.1", false)]
    [InlineData("2001:db8::/32", "2001:db8:ffff::1", true)]
    [InlineData("2001:db8::/32", "2001:db9::1", false)]
    [InlineData("0.0.0.0/0", "2001:db8::1", false)]       // IPv4 网段不匹配 IPv6 地址
    [InlineData("::/0", "203.0.113.10", false)]           // IPv6 网段不匹配 IPv4 地址
    [InlineData("broken", "203.0.113.10", false)]         // 损坏的条目按不命中处理
    public void Matches_ComparesWithinSameAddressFamily(string entry, string client, bool expected)
    {
        Assert.Equal(expected, IpWhitelistMatcher.Matches(entry, IPAddress.Parse(client)));
    }

    [Fact]
    public void Normalize_ConvertsIpv4MappedAddress()
    {
        var mapped = IPAddress.Parse("::ffff:203.0.113.10");

        Assert.Equal(IPAddress.Parse("203.0.113.10"), IpWhitelistMatcher.Normalize(mapped));
        Assert.True(IpWhitelistMatcher.Matches("203.0.113.0/24", IpWhitelistMatcher.Normalize(mapped)));
    }
}
