using Suanpan.Core.Common.Auditing;
using Suanpan.Core.Sys.Enums;

namespace Suanpan.Core.Sys.Entities;

/// <summary>后台账号（sys_user），只有内部运营人员。</summary>
public class SysUser : AuditedEntity
{
    public long Id { get; set; }

    /// <summary>登录名，唯一，创建后不可修改</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>密码哈希，自描述格式 pbkdf2$迭代次数$盐$哈希</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string RealName { get; set; } = string.Empty;

    public string? Email { get; set; }

    /// <summary>账号状态（sys 设计 §4.1）；锁定不是单独的状态，见 <see cref="LockedUntil"/></summary>
    public UserStatus Status { get; set; } = UserStatus.Pending;

    /// <summary>首次登录或管理员重置密码后为 true，登录后必须先改密码</summary>
    public bool MustChangePassword { get; set; } = true;

    /// <summary>AES 加密后的 TOTP 密钥（enc: 前缀）；为空表示尚未绑定</summary>
    public string? TotpSecret { get; set; }

    /// <summary>安全戳：改密码、重置密码、停用、绑定或重置 TOTP 时刷新；Cookie 携带副本，逐请求比对</summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>连续登录失败次数，密码和 TOTP 共用（sys 设计 §4.3）</summary>
    public int LoginFailCount { get; set; }

    /// <summary>锁定截止时间（UTC）；为空或早于当前时间表示未锁定</summary>
    public DateTime? LockedUntil { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public string? LastLoginIp { get; set; }
}
