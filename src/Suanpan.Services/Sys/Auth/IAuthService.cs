using Suanpan.Services.Sys.Auth.Models;

namespace Suanpan.Services.Sys.Auth;

/// <summary>
/// 后台登录（sys 设计 §5.1）：密码 →（必须改密码时）改密码 →（未绑定时）绑定 TOTP → 输入验证码。
/// 密码通过后到完成之前都在待完成 Cookie 下进行，每一步都按账号当前的状态判断，不能跳步；
/// 只通过了密码的人不能主动改密码、不能改绑 TOTP。每一步都写登录日志，库里记真实原因，页面上统一提示。
/// 密码错误和登录过程中的 TOTP 错误共用一个失败计数，达到上限时锁定；锁定期间密码正确也拒绝；登录成功时清零（sys 设计 §4.3）。
/// </summary>
public interface IAuthService
{
    /// <summary>第一步：登录名和密码。成功后由调用方签发待完成 Cookie，进入 <see cref="AuthResult.Next"/></summary>
    Task<AuthResult> PasswordSignInAsync(string username, string password, LoginClient client, CancellationToken ct = default);

    /// <summary>账号当前应进行的下一步；账号不存在或已停用时失败</summary>
    Task<AuthResult> GetProgressAsync(long userId, CancellationToken ct = default);

    /// <summary>
    /// 改密码。signedIn 为 true 表示已完成登录（主动修改，须校验当前密码）；为 false 表示在待完成 Cookie 下强制修改，
    /// 只在账号必须改密码时允许。新密码要符合密码规则且不能与旧密码相同；完成后刷新安全戳，其他会话失效。
    /// </summary>
    Task<AuthResult> ChangePasswordAsync(long userId, string? currentPassword, string newPassword, bool signedIn, LoginClient client, CancellationToken ct = default);

    /// <summary>生成待绑定的 TOTP 密钥</summary>
    TotpSetup CreateTotpSetup(string username);

    /// <summary>从页面提交回来的加密密钥恢复绑定数据（表单校验失败时重新显示同一个二维码）；密文无效时返回 null</summary>
    TotpSetup? RestoreTotpSetup(string protectedSecret, string username);

    /// <summary>
    /// 绑定 TOTP：用待绑定的密钥验证一次验证码后保存。signedIn 为 false（待完成 Cookie）时只在尚未绑定时允许，
    /// 待激活的账号此时变为启用。完成后刷新安全戳，其他会话失效。
    /// </summary>
    Task<AuthResult> ConfirmTotpSetupAsync(long userId, string protectedSecret, string code, bool signedIn, LoginClient client, CancellationToken ct = default);

    /// <summary>最后一步：输入 TOTP 验证码，通过后由调用方签发正式 Cookie</summary>
    Task<AuthResult> VerifyTotpAsync(long userId, string code, LoginClient client, CancellationToken ct = default);

    /// <summary>退出：写登录日志</summary>
    Task LogoutAsync(long userId, string username, LoginClient client, CancellationToken ct = default);
}
