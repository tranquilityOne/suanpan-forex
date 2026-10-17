using Suanpan.Shared.Auth;
using Xunit;

namespace Suanpan.Shared.Tests.Auth;

public class PermCodeHelperTests
{
    [Theory]
    [InlineData("Sys", "OpLog", "Export", "sys:op_log:export")]
    [InlineData("Calendar", "Calendar", "Forecast", "calendar:calendar:forecast")]
    [InlineData("Sys", "IpWhitelist", "Index", "sys:ip_whitelist:index")]
    [InlineData("Collect", "CollectTask", "TriggerNow", "collect:collect_task:trigger_now")]
    public void Derive_JoinsThreeSnakeCaseSegments(string area, string controller, string action, string expected)
    {
        Assert.Equal(expected, PermCodeHelper.Derive(area, controller, action));
    }

    [Theory]
    [InlineData("Sys", "sys")]
    [InlineData("OpLog", "op_log")]
    [InlineData("Index", "index")]
    [InlineData("Fx2Rate", "fx2_rate")]
    [InlineData("ABC", "abc")]
    [InlineData("", "")]
    public void ToSnake_SplitsOnlyBeforeUppercaseThatFollowsNonUppercase(string pascal, string expected)
    {
        Assert.Equal(expected, PermCodeHelper.ToSnake(pascal));
    }

    [Theory]
    [InlineData("Index", "列表")]
    [InlineData("Export", "导出")]
    [InlineData("Rebuild", "Rebuild")]
    public void DefaultActionName_UsesBuiltInNamesOrFallsBackToAction(string action, string expected)
    {
        Assert.Equal(expected, PermCodeHelper.DefaultActionName(action));
    }
}
