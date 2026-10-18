using Suanpan.Core.Sys.Rules;
using Xunit;

namespace Suanpan.Core.Tests.Sys.Rules;

/// <summary>完成标准（JAN-72）：第 4、5、6 次失败的状态，以及锁定期满后重新计数。上限 5 次，锁定 15 分钟</summary>
public class LockoutRuleTests
{
    private const int MaxFailures = 5;

    private static readonly DateTime Now = new(2026, 10, 18, 1, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    [Fact]
    public void FourthFailure_IsCountedButNotLocked()
        => Assert.Equal(new LockoutState(4, null), LockoutRule.AfterFailure(3, null, Now, MaxFailures, LockDuration));

    [Fact]
    public void FifthFailure_LocksFor15Minutes()
        => Assert.Equal(new LockoutState(5, Now + LockDuration), LockoutRule.AfterFailure(4, null, Now, MaxFailures, LockDuration));

    /// <summary>第 6 次在锁定期内：仍是锁定状态（登录被拒绝，不再计数），提示剩余时间</summary>
    [Fact]
    public void SixthAttempt_WithinLock_IsLocked()
    {
        var lockedUntil = Now + LockDuration;
        var sixthAttempt = Now.AddMinutes(10).AddSeconds(30);

        Assert.True(LockoutRule.IsLocked(lockedUntil, sixthAttempt));
        Assert.Equal(5, LockoutRule.RemainingMinutes(lockedUntil, sixthAttempt));
    }

    [Fact]
    public void FirstFailureAfterLockExpires_RestartsFromOne()
        => Assert.Equal(new LockoutState(1, null), LockoutRule.AfterFailure(MaxFailures, Now, Now, MaxFailures, LockDuration));

    [Theory]
    [InlineData(null, false)]
    [InlineData(1, true)]
    [InlineData(0, false)]   // 截止时间等于当前时间：已解除
    public void IsLocked_OnlyBeforeLockedUntil(int? secondsUntilUnlock, bool expected)
        => Assert.Equal(expected, LockoutRule.IsLocked(secondsUntilUnlock is { } s ? Now.AddSeconds(s) : null, Now));
}
