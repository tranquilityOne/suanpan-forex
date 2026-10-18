using System.Globalization;
using Suanpan.Core.Sys.Enums;
using Suanpan.Services.Sys.Configs.Models;

namespace Suanpan.Services.Sys.Configs;

/// <summary>
/// Sys 模块的配置键和默认值（sys 设计 §10.2）。读取时把默认值传给 IConfigService，库里有值时覆盖：
/// <c>config.GetIntAsync(SysConfigKeys.LoginMaxFailures, SysConfigKeys.LoginMaxFailuresDefault)</c>。
/// 新增配置项时同时加入 <see cref="Definitions"/>，系统配置页面才能显示和修改。
/// </summary>
public static class SysConfigKeys
{
    private const string SecurityGroup = "安全";

    /// <summary>连续登录失败多少次后锁定</summary>
    public const string LoginMaxFailures = "security:login_max_failures";
    public const int LoginMaxFailuresDefault = 5;

    /// <summary>锁定时长（分钟）</summary>
    public const string LoginLockMinutes = "security:login_lock_minutes";
    public const int LoginLockMinutesDefault = 15;

    /// <summary>后台 IP 白名单总开关</summary>
    public const string AdminIpWhitelistEnabled = "security:admin_ip_whitelist_enabled";
    public const bool AdminIpWhitelistEnabledDefault = false;

    public static readonly IReadOnlyList<ConfigDefinition> Definitions =
    [
        new(LoginMaxFailures, ConfigValueType.Int, Text(LoginMaxFailuresDefault), SecurityGroup, "连续登录失败多少次后锁定账号"),
        new(LoginLockMinutes, ConfigValueType.Int, Text(LoginLockMinutesDefault), SecurityGroup, "账号锁定时长（分钟）"),
        new(AdminIpWhitelistEnabled, ConfigValueType.Bool, Text(AdminIpWhitelistEnabledDefault), SecurityGroup, "是否只允许 IP 白名单中的地址访问后台"),
    ];

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Text(bool value) => value ? "true" : "false";
}
