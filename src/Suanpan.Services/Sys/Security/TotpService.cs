using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Suanpan.Services.Sys.Security;

/// <summary>
/// RFC 6238（TOTP）+ RFC 4226（HOTP 动态截断），不依赖第三方库。
/// 正确性用 RFC 6238 附录 B 的测试向量验证（Suanpan.Services.Tests）。
/// </summary>
public sealed class TotpService(TimeProvider timeProvider) : ITotpService
{
    private const int SecretBytes = 20;
    private const int StepSeconds = 30;
    private const int Digits = 6;
    private const int WindowTolerance = 1;

    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public string GenerateSecret() => Base32Encode(RandomNumberGenerator.GetBytes(SecretBytes));

    public string BuildOtpauthUri(string issuer, string account, string secret)
        => $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}"
           + $"?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";

    public bool VerifyCode(string secret, string code, out long matchedStep)
    {
        matchedStep = 0;
        code = code?.Trim() ?? string.Empty;
        if (code.Length != Digits || !code.All(char.IsAsciiDigit))
        {
            return false;
        }

        byte[] key;
        try
        {
            key = Base32Decode(secret);
        }
        catch (FormatException)
        {
            return false;
        }

        var currentStep = timeProvider.GetUtcNow().ToUnixTimeSeconds() / StepSeconds;
        var codeBytes = Encoding.ASCII.GetBytes(code);
        for (var offset = -WindowTolerance; offset <= WindowTolerance; offset++)
        {
            var step = currentStep + offset;
            var expected = Encoding.ASCII.GetBytes(ComputeCode(key, step));
            if (CryptographicOperations.FixedTimeEquals(expected, codeBytes))
            {
                matchedStep = step;
                return true;
            }
        }

        return false;
    }

    /// <summary>HOTP：HMAC-SHA1(key, 时间步) → 动态截断 → 取模并补足位数</summary>
    private static string ComputeCode(byte[] key, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.HashData(key, counter, hash);

        var offset = hash[19] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
                     | (hash[offset + 1] << 16)
                     | (hash[offset + 2] << 8)
                     | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    // Base32（RFC 4648，无填充）

    private static string Base32Encode(byte[] data)
    {
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                sb.Append(Base32Alphabet[(buffer >> bits) & 0x1F]);
            }
        }

        if (bits > 0)
        {
            sb.Append(Base32Alphabet[(buffer << (5 - bits)) & 0x1F]);
        }

        return sb.ToString();
    }

    private static byte[] Base32Decode(string encoded)
    {
        var clean = encoded.Trim().TrimEnd('=').ToUpperInvariant();
        if (clean.Length == 0)
        {
            throw new FormatException("密钥为空");
        }

        var result = new List<byte>(clean.Length * 5 / 8);
        int buffer = 0, bits = 0;
        foreach (var c in clean)
        {
            var index = Base32Alphabet.IndexOf(c);
            if (index < 0)
            {
                throw new FormatException($"非法的 Base32 字符：{c}");
            }

            buffer = (buffer << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                result.Add((byte)((buffer >> bits) & 0xFF));
            }
        }

        return [.. result];
    }
}
