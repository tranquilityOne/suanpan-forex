namespace Suanpan.Services.Sys.Security;

/// <summary>
/// 敏感数据加密（TOTP 密钥、数据源凭据），保存前必须加密。密文格式 "enc:..."。
/// </summary>
public interface ISecretProtector
{
    string Encrypt(string plaintext);

    /// <summary>解密；不是 enc: 开头的值会抛 <see cref="FormatException"/>，密文被篡改会抛 <see cref="System.Security.Cryptography.CryptographicException"/></summary>
    string Decrypt(string ciphertext);

    /// <summary>页面展示用的掩码：保留首尾各 4 位，中间用 * 代替</summary>
    static string Mask(string? value)
        => string.IsNullOrEmpty(value) ? string.Empty
            : value.Length <= 8 ? "********"
            : $"{value[..4]}****{value[^4..]}";
}
