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

    [Theory]
    [InlineData("US.NFP", "US")]
    [InlineData("US.CPI.YOY", "US")]          // 多段
    [InlineData("US.T10Y", "US")]             // 字母和数字
    [InlineData("EA.HICP.CORE.YOY.FINAL", "EA")]
    public void IndicatorCode_AcceptsEconomyDotShortName(string code, string economyCode)
        => Assert.Null(ReferenceCodeRule.ValidateIndicatorCode(code, economyCode));

    [Theory]
    [InlineData(null)]
    [InlineData("US")]                        // 没有简称
    [InlineData("US.")]
    [InlineData("US..CPI")]                   // 空段
    [InlineData("us.cpi")]                    // 未转大写
    [InlineData("US.CPI-YOY")]                // 含其他字符
    [InlineData("USA.CPI")]                   // 经济体段不是 2 个字母
    [InlineData("US.CPI\n")]                  // 末尾换行
    [InlineData("US.ABCDEFGHIJKLMNOPQRSTUVWXYZ1234")]  // 33 个字符，超过列长
    public void IndicatorCode_RejectsOtherFormats(string? code)
        => Assert.NotNull(ReferenceCodeRule.ValidateIndicatorCode(code, "US"));

    /// <summary>BR-029：编码必须以所属经济体开头，德国的指标不能用 EA 开头</summary>
    [Fact]
    public void IndicatorCode_MustStartWithItsEconomy()
        => Assert.NotNull(ReferenceCodeRule.ValidateIndicatorCode("EA.IFO", "DE"));

    [Fact]
    public void IndicatorCode_AcceptsMaxLength()
        => Assert.Null(ReferenceCodeRule.ValidateIndicatorCode("US." + new string('A', ReferenceCodeRule.MaxIndicatorCodeLength - 3), "US"));

    /// <summary>前台指标详情页的地址（web-portal BR-015）</summary>
    [Theory]
    [InlineData("US.NFP", "us-nfp")]
    [InlineData("US.CPI.YOY", "us-cpi-yoy")]
    public void ToSlug_LowercasesAndReplacesDots(string code, string slug)
        => Assert.Equal(slug, ReferenceCodeRule.ToSlug(code));
}
