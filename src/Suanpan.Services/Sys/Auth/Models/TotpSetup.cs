namespace Suanpan.Services.Sys.Auth.Models;

/// <summary>
/// 绑定 TOTP 的页面数据：ProtectedSecret 是加密后的待绑定密钥，放在页面隐藏域里，验证通过后才保存（sys 设计 §5.4）；
/// Secret 和 OtpauthUri 用于显示二维码和手动输入。
/// </summary>
public sealed record TotpSetup(string ProtectedSecret, string Secret, string OtpauthUri);
