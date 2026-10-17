namespace Suanpan.Core.Sys.Enums;

/// <summary>登录日志记录的步骤（sys 设计 §7.2）</summary>
public enum LoginStage : short
{
    /// <summary>密码验证</summary>
    Password = 1,

    /// <summary>TOTP 验证</summary>
    Totp = 2,

    /// <summary>激活：首次登录改密码并绑定 TOTP</summary>
    Activation = 3,

    /// <summary>退出</summary>
    Logout = 4,
}
