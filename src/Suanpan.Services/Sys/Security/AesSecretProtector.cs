using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Suanpan.Services.Sys.Security;

/// <summary>
/// AES-256-GCM 加密。密钥取配置 Security:EncryptionKey：32 字节随机数的 Base64（如 openssl rand -base64 32），
/// 本地放 user-secrets，其他环境用环境变量 SUANPAN__Security__EncryptionKey，不进代码仓库。
/// 密文格式：enc:Base64(nonce[12] | tag[16] | 密文)。
/// </summary>
public sealed class AesSecretProtector : ISecretProtector
{
    private const string Prefix = "enc:";
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public AesSecretProtector(IConfiguration configuration)
    {
        var configured = configuration["Security:EncryptionKey"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "缺少配置 Security:EncryptionKey（32 字节随机数的 Base64）。本地用 user-secrets 设置，其他环境用环境变量 SUANPAN__Security__EncryptionKey（见 README）。");
        }

        try
        {
            _key = Convert.FromBase64String(configured);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("配置 Security:EncryptionKey 不是合法的 Base64。");
        }

        if (_key.Length != KeySize)
        {
            throw new InvalidOperationException($"配置 Security:EncryptionKey 解码后应为 {KeySize} 字节，实际为 {_key.Length} 字节。");
        }
    }

    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        var packed = new byte[NonceSize + TagSize + cipher.Length];
        nonce.CopyTo(packed, 0);
        tag.CopyTo(packed, NonceSize);
        cipher.CopyTo(packed, NonceSize + TagSize);
        return Prefix + Convert.ToBase64String(packed);
    }

    public string Decrypt(string ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        if (!ciphertext.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new FormatException("不是加密后的值（缺少 enc: 前缀）。");
        }

        var packed = Convert.FromBase64String(ciphertext[Prefix.Length..]);
        if (packed.Length < NonceSize + TagSize)
        {
            throw new FormatException("密文长度不正确。");
        }

        var nonce = packed.AsSpan(0, NonceSize);
        var tag = packed.AsSpan(NonceSize, TagSize);
        var cipher = packed.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
