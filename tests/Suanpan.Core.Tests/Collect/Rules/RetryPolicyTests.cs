using Suanpan.Core.Collect.Rules;
using Xunit;

namespace Suanpan.Core.Tests.Collect.Rules;

/// <summary>BR-009：最多重试 3 次，默认间隔 30 秒、2 分钟、5 分钟（可配置）</summary>
public class RetryPolicyTests
{
    [Fact]
    public void DefaultDelays_AreThreeRetries()
        => Assert.Equal([TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5)], RetryPolicy.ParseDelays(RetryPolicy.DefaultDelaySeconds));

    [Theory]
    [InlineData("", 0)]                  // 不重试
    [InlineData(" 0, 0 ", 2)]            // 测试环境：立即重试
    public void ParseDelays_AcceptsEmptyAndZero(string value, int count)
        => Assert.Equal(count, RetryPolicy.ParseDelays(value)!.Count);

    [Theory]
    [InlineData("30,abc")]
    [InlineData("30,-5")]
    [InlineData("30,,120")]
    [InlineData("1.5")]
    public void ParseDelays_RejectsMalformedValues(string value)
        => Assert.Null(RetryPolicy.ParseDelays(value));

    [Fact]
    public void DelayAfter_UsesTheNthDelayThenStops()
    {
        var delays = RetryPolicy.ParseDelays(RetryPolicy.DefaultDelaySeconds)!;

        Assert.Equal(TimeSpan.FromSeconds(30), RetryPolicy.DelayAfter(1, delays));
        Assert.Equal(TimeSpan.FromMinutes(5), RetryPolicy.DelayAfter(3, delays));
        Assert.Null(RetryPolicy.DelayAfter(4, delays));          // 第 4 次请求失败后不再重试：最多重试 3 次
    }

    /// <summary>超时或网络错误、5xx、429 可以重试；其他 4xx 不重试</summary>
    [Theory]
    [InlineData(null, true)]
    [InlineData(500, true)]
    [InlineData(503, true)]
    [InlineData(429, true)]
    [InlineData(403, false)]
    [InlineData(404, false)]
    [InlineData(400, false)]
    public void IsRetryable_FollowsStatusCode(int? status, bool retryable)
        => Assert.Equal(retryable, RetryPolicy.IsRetryable(status));
}
