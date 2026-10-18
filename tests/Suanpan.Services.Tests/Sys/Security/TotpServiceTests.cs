using Suanpan.Services.Sys.Security;
using Xunit;

namespace Suanpan.Services.Tests.Sys.Security;

public class TotpServiceTests
{
    // RFC 6238 附录 B 的 SHA1 密钥 "12345678901234567890" 的 Base32 编码
    private const string RfcSecret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    /// <summary>RFC 6238 附录 B 的 SHA1 测试向量（8 位验证码取后 6 位）</summary>
    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    [InlineData(20000000000L, "353130")]
    public void VerifyCode_MatchesRfc6238TestVectors(long unixSeconds, string code)
    {
        var totp = new TotpService(new FixedTimeProvider(unixSeconds));

        Assert.True(totp.VerifyCode(RfcSecret, code, out var step));
        Assert.Equal(unixSeconds / 30, step);
    }

    [Theory]
    [InlineData(0L, true)]
    [InlineData(30L, true)]    // 验证码属于上一个时间步：允许
    [InlineData(-30L, true)]   // 验证码属于下一个时间步：允许
    [InlineData(60L, false)]   // 相差 2 个时间步：拒绝
    [InlineData(-60L, false)]
    public void VerifyCode_AllowsOneStepEitherSide(long offsetSeconds, bool expected)
    {
        // RFC 6238 向量：1111111109 秒时的验证码为 081804
        var totp = new TotpService(new FixedTimeProvider(1111111109L + offsetSeconds));

        Assert.Equal(expected, totp.VerifyCode(RfcSecret, "081804", out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("28708")]
    [InlineData("2870820")]
    [InlineData("28708a")]
    public void VerifyCode_RejectsMalformedCode(string code)
    {
        Assert.False(new TotpService(new FixedTimeProvider(59)).VerifyCode(RfcSecret, code, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-base32!")]
    public void VerifyCode_RejectsMalformedSecret(string secret)
    {
        Assert.False(new TotpService(new FixedTimeProvider(59)).VerifyCode(secret, "287082", out _));
    }

    [Fact]
    public void GenerateSecret_Returns32Base32Characters()
    {
        var secret = new TotpService(TimeProvider.System).GenerateSecret();

        Assert.Equal(32, secret.Length);
        Assert.Matches("^[A-Z2-7]{32}$", secret);
    }

    [Fact]
    public void BuildOtpauthUri_EscapesIssuerAndAccount()
    {
        var uri = new TotpService(TimeProvider.System).BuildOtpauthUri("算盘 后台", "zhang san", RfcSecret);

        Assert.StartsWith("otpauth://totp/", uri);
        Assert.Contains($"secret={RfcSecret}", uri);
        Assert.Contains("digits=6", uri);
        Assert.Contains("period=30", uri);
        Assert.DoesNotContain(" ", uri);
    }

    private sealed class FixedTimeProvider(long unixSeconds) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
    }
}
