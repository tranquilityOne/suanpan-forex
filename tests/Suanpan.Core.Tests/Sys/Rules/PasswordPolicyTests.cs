using Suanpan.Core.Sys.Rules;
using Xunit;

namespace Suanpan.Core.Tests.Sys.Rules;

public class PasswordPolicyTests
{
    [Fact]
    public void Validate_AcceptsLongPasswordWithLettersAndDigits()
    {
        Assert.Null(PasswordPolicy.Validate("abcdefghij12"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("abcdefghi12")]   // 11 位
    public void Validate_RejectsShortPassword(string? password)
    {
        Assert.Equal("密码至少 12 位", PasswordPolicy.Validate(password));
    }

    [Theory]
    [InlineData("abcdefghijkl")]   // 没有数字
    [InlineData("123456789012")]   // 没有字母
    [InlineData("密码密码密码密码密码12")] // 中文不算英文字母
    public void Validate_RequiresBothLettersAndDigits(string password)
    {
        Assert.Equal("密码必须同时包含英文字母和数字", PasswordPolicy.Validate(password));
    }
}
