using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Suanpan.Services.Sys.Security;
using Xunit;

namespace Suanpan.Services.Tests.Sys.Security;

public class AesSecretProtectorTests
{
    // 每次运行随机生成，不出现固定密钥
    private readonly string _key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void Encrypt_ThenDecrypt_ReturnsOriginal()
    {
        var protector = Create(_key);

        var cipher = protector.Encrypt("JBSWY3DPEHPK3PXP 中文");

        Assert.StartsWith("enc:", cipher);
        Assert.Equal("JBSWY3DPEHPK3PXP 中文", protector.Decrypt(cipher));
    }

    [Fact]
    public void Encrypt_UsesRandomNonce()
    {
        var protector = Create(_key);

        Assert.NotEqual(protector.Encrypt("same"), protector.Encrypt("same"));
    }

    [Fact]
    public void Decrypt_WithDifferentKey_Fails()
    {
        var cipher = Create(_key).Encrypt("secret");
        var other = Create(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

        Assert.ThrowsAny<CryptographicException>(() => other.Decrypt(cipher));
    }

    [Fact]
    public void Decrypt_TamperedCipher_Fails()
    {
        var protector = Create(_key);
        var packed = Convert.FromBase64String(protector.Encrypt("secret")["enc:".Length..]);
        packed[^1] ^= 0x01;

        Assert.ThrowsAny<CryptographicException>(() => protector.Decrypt("enc:" + Convert.ToBase64String(packed)));
    }

    [Theory]
    [InlineData("plain-text")]
    [InlineData("enc:AAAA")]
    public void Decrypt_RejectsValuesThatAreNotCiphertext(string value)
    {
        Assert.Throws<FormatException>(() => Create(_key).Decrypt(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA==")] // 16 字节，长度不对
    public void Constructor_RejectsMissingOrInvalidKey(string? key)
    {
        Assert.Throws<InvalidOperationException>(() => Create(key));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("short", "********")]
    [InlineData("abcdefghijkl", "abcd****ijkl")]
    public void Mask_KeepsOnlyFirstAndLastFourCharacters(string? value, string expected)
    {
        Assert.Equal(expected, ISecretProtector.Mask(value));
    }

    private static AesSecretProtector Create(string? key) =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Security:EncryptionKey"] = key })
            .Build());
}
