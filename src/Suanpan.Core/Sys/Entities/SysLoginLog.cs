using Suanpan.Core.Sys.Enums;

namespace Suanpan.Core.Sys.Entities;

/// <summary>
/// 登录日志（sys_login_log）：只追加，成功和失败都记；库里记真实原因，页面上统一提示（sys 设计 §5.1、§7.2）。
/// </summary>
public class SysLoginLog
{
    public long Id { get; set; }

    /// <summary>输入的登录名，账号不存在时也记录</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>账号存在时回填</summary>
    public long? UserId { get; set; }

    public LoginStage Stage { get; set; }

    public bool IsSuccess { get; set; }

    /// <summary>真实的失败原因，如"账号不存在""密码错误""已锁定""TOTP 错误""TOTP 重放"</summary>
    public string? FailReason { get; set; }

    public string? Ip { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }
}
