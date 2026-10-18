namespace Suanpan.Services.Sys.Security;

/// <summary>
/// TOTP（RFC 6238）：HMAC-SHA1、30 秒一个时间步、6 位验证码、允许前后各 1 个时间步（sys 设计 §5.4）。
/// 本服务只做算法；防重放和失败计数由认证服务负责。
/// </summary>
public interface ITotpService
{
    /// <summary>生成新密钥：20 个随机字节的 Base32 编码（32 个字符），验证通过后才保存</summary>
    string GenerateSecret();

    /// <summary>验证器 App 扫码用的 otpauth:// 地址</summary>
    string BuildOtpauthUri(string issuer, string account, string secret);

    /// <summary>
    /// 校验 6 位验证码。通过时 <paramref name="matchedStep"/> 返回命中的时间步，
    /// 调用方用它防重放（同一个时间步的验证码只能用一次）。
    /// </summary>
    bool VerifyCode(string secret, string code, out long matchedStep);
}
