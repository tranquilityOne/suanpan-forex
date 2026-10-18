using Suanpan.Services.Sys.Security;
using Xunit;

namespace Suanpan.Services.Tests.Sys.Security;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_UsesSelfDescribingFormat()
    {
        var parts = _hasher.Hash("Correct-Horse-1").Split('$');

        Assert.Equal(4, parts.Length);
        Assert.Equal("pbkdf2", parts[0]);
        Assert.Equal("100000", parts[1]);
        Assert.Equal(16, Convert.FromBase64String(parts[2]).Length);
        Assert.Equal(32, Convert.FromBase64String(parts[3]).Length);
    }

    [Fact]
    public void Hash_UsesRandomSalt()
    {
        Assert.NotEqual(_hasher.Hash("Correct-Horse-1"), _hasher.Hash("Correct-Horse-1"));
    }

    [Fact]
    public void Verify_AcceptsOnlyTheOriginalPassword()
    {
        var hash = _hasher.Hash("Correct-Horse-1");

        Assert.True(_hasher.Verify("Correct-Horse-1", hash));
        Assert.False(_hasher.Verify("correct-horse-1", hash));
        Assert.False(_hasher.Verify("", hash));
    }

    [Fact]
    public void Verify_UsesIterationsStoredInHash()
    {
        // 迭代次数记在哈希里：以后上调默认值，旧哈希仍能校验
        var parts = _hasher.Hash("Correct-Horse-1").Split('$');
        var salt = Convert.FromBase64String(parts[2]);
        var hash = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
            "Correct-Horse-1", salt, 1000, System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
        var oldFormat = $"pbkdf2$1000${parts[2]}${Convert.ToBase64String(hash)}";

        Assert.True(_hasher.Verify("Correct-Horse-1", oldFormat));
    }

    [Theory]
    [InlineData("plain-text")]
    [InlineData("pbkdf2$abc$AAAA$AAAA")]
    [InlineData("pbkdf2$1000$not-base64$AAAA")]
    public void Verify_ReturnsFalseForMalformedHash(string malformed)
    {
        Assert.False(_hasher.Verify("Correct-Horse-1", malformed));
    }
}
