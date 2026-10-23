namespace Suanpan.Crm.Auth;

/// <summary>
/// 开发环境跳过 TOTP 验证码（sys 设计 §5.1）：只有 Development 环境、并且配置 Auth:SkipTotpInDevelopment 为 true 时开启，
/// 其他环境配了也不生效（BR-140：正式登录必须二次验证）。开启后只跳过已绑定账号的"输入验证码"，
/// 改密码、绑定 TOTP 照常；登录日志的步骤记为"跳过 TOTP"。
/// </summary>
public sealed class TotpSkipSwitch(bool enabled, bool ignored)
{
    public const string ConfigKey = "Auth:SkipTotpInDevelopment";

    public bool Enabled { get; } = enabled;

    /// <summary>非开发环境配置了开关而被忽略，启动时记警告</summary>
    public bool Ignored { get; } = ignored;

    public static TotpSkipSwitch From(IConfiguration configuration, IHostEnvironment environment)
    {
        var requested = configuration.GetValue(ConfigKey, false);
        return new TotpSkipSwitch(requested && environment.IsDevelopment(), requested && !environment.IsDevelopment());
    }
}
