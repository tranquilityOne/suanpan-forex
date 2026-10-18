using Suanpan.Core.Sys.Rules;
using Xunit;

namespace Suanpan.Core.Tests.Sys.Rules;

public class PermCodePatternTests
{
    [Theory]
    [InlineData("calendar:*:index", "calendar:event:index", true)]
    [InlineData("calendar:*:index", "calendar:event:edit", false)]
    [InlineData("calendar:forecast:*", "calendar:forecast:save", true)]
    [InlineData("calendar:event:index", "calendar:event:index", true)]
    [InlineData("calendar:event:index", "Calendar:event:index", false)]  // 区分大小写
    public void IsMatch_ComparesEachSegment(string pattern, string code, bool expected)
    {
        Assert.Equal(expected, PermCodePattern.IsMatch(pattern, code));
    }

    [Theory]
    [InlineData("calendar:*", "calendar:event:index")]  // 规则不是三段
    [InlineData("*:*:*", "calendar")]                   // 目录的权限码只有一段
    public void IsMatch_RequiresThreeSegments(string pattern, string code)
    {
        Assert.False(PermCodePattern.IsMatch(pattern, code));
    }
}
