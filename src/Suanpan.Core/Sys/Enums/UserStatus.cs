namespace Suanpan.Core.Sys.Enums;

/// <summary>后台账号状态（sys 设计 §4.1）。锁定不是单独的状态，见 SysUser.LockedUntil。</summary>
public enum UserStatus : short
{
    /// <summary>待激活：新建或初始管理员，首次登录须改密码并绑定 TOTP</summary>
    Pending = 1,

    /// <summary>启用</summary>
    Active = 2,

    /// <summary>停用</summary>
    Disabled = 3,
}
