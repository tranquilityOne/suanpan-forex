namespace Suanpan.Shared.Auth;

/// <summary>
/// 后台（Crm）的 Cookie 认证 scheme，同时用作 Cookie 名（sys 设计 §5.2）。
/// </summary>
public static class AuthSchemes
{
    /// <summary>正式登录：空闲 30 分钟超时（滑动）。</summary>
    public const string Crm = "suanpan.crm.auth";

    /// <summary>
    /// 待完成：密码已通过，尚未完成改密码或 TOTP 验证。10 分钟有效、不滑动，不带任何业务权限，
    /// 只能访问改密码、绑定 TOTP、TOTP 验证三个页面。
    /// </summary>
    public const string CrmPending = "suanpan.crm.pending";
}
