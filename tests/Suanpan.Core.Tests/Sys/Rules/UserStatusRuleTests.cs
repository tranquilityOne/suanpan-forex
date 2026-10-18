using Suanpan.Core.Sys.Rules;
using Xunit;

namespace Suanpan.Core.Tests.Sys.Rules;

public class UserStatusRuleTests
{
    [Fact]
    public void CanDisable_RejectsSelf()
    {
        Assert.Equal("不能停用自己", UserStatusRule.CanDisable(7, currentUserId: 7, targetIsActiveAdmin: false, activeAdminCount: 3));
    }

    [Fact]
    public void CanDisable_RejectsLastAdmin()
    {
        Assert.Equal("不能停用最后一个管理员", UserStatusRule.CanDisable(7, currentUserId: 1, targetIsActiveAdmin: true, activeAdminCount: 1));
    }

    [Theory]
    [InlineData(true, 2)]   // 还有其他管理员
    [InlineData(false, 1)]  // 停用的不是管理员
    public void CanDisable_AllowsOtherCases(bool targetIsActiveAdmin, int activeAdminCount)
    {
        Assert.Null(UserStatusRule.CanDisable(7, currentUserId: 1, targetIsActiveAdmin, activeAdminCount));
    }

    [Fact]
    public void CanDisable_AllowsWhenNoCurrentUser()
    {
        Assert.Null(UserStatusRule.CanDisable(7, currentUserId: null, targetIsActiveAdmin: false, activeAdminCount: 1));
    }

    [Theory]
    [InlineData(true, 1, false)]
    [InlineData(true, 2, true)]
    [InlineData(false, 1, true)]
    public void CanRemoveAdminRole_KeepsAtLeastOneAdmin(bool targetIsActiveAdmin, int activeAdminCount, bool allowed)
    {
        Assert.Equal(allowed, UserStatusRule.CanRemoveAdminRole(targetIsActiveAdmin, activeAdminCount) is null);
    }
}
