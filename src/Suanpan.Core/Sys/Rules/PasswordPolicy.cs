namespace Suanpan.Core.Sys.Rules;

/// <summary>
/// 密码规则（sys 设计 §4.2，BR-140）：长度至少 12 位，同时包含英文字母和数字。
/// "新密码不能与旧密码相同"需要比对哈希，由改密码流程判断（ENG-005.1）。
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 12;

    /// <summary>返回 null 表示符合规则，否则返回面向用户的提示</summary>
    public static string? Validate(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
        {
            return $"密码至少 {MinLength} 位";
        }

        if (!password.Any(char.IsAsciiLetter) || !password.Any(char.IsAsciiDigit))
        {
            return "密码必须同时包含英文字母和数字";
        }

        return null;
    }
}
