using Suanpan.Core.Reference.Rules;
using Xunit;

namespace Suanpan.Core.Tests.Reference.Rules;

public class ReferenceCodeRuleTests
{
    [Fact]
    public void Normalize_TrimsAndUppercases()
        => Assert.Equal("USD", ReferenceCodeRule.Normalize(" usd "));

    [Theory]
    [InlineData("USD")]
    [InlineData("EUR")]
    public void CurrencyCode_AcceptsThreeUppercaseLetters(string code)
        => Assert.Null(ReferenceCodeRule.ValidateCurrencyCode(code));

    [Theory]
    [InlineData(null)]
    [InlineData("US")]       // 太短
    [InlineData("USDT")]     // 太长
    [InlineData("usd")]      // 未转大写
    [InlineData("U5D")]      // 含数字
    [InlineData("USD\n")]    // 末尾换行
    public void CurrencyCode_RejectsOtherFormats(string? code)
        => Assert.NotNull(ReferenceCodeRule.ValidateCurrencyCode(code));

    [Theory]
    [InlineData("US")]
    [InlineData("EA")]       // 欧元区
    public void EconomyCode_AcceptsTwoUppercaseLetters(string code)
        => Assert.Null(ReferenceCodeRule.ValidateEconomyCode(code));

    [Theory]
    [InlineData(null)]
    [InlineData("U")]
    [InlineData("USA")]
    [InlineData("us")]
    public void EconomyCode_RejectsOtherFormats(string? code)
        => Assert.NotNull(ReferenceCodeRule.ValidateEconomyCode(code));
}
