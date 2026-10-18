namespace Suanpan.Core.Sys.Enums;

/// <summary>密码通过后、进入后台之前还要完成的步骤（sys 设计 §5.1）</summary>
public enum SignInStep
{
    /// <summary>首次登录或管理员重置密码后，必须先改密码</summary>
    ChangePassword = 1,

    /// <summary>还没有绑定 TOTP（新账号，或管理员重置了二次验证）</summary>
    SetupTotp = 2,

    /// <summary>输入 TOTP 验证码</summary>
    VerifyTotp = 3,

    /// <summary>全部完成，签发正式 Cookie</summary>
    Completed = 4,
}
