namespace Suanpan.Core.Sys.Rules;

/// <summary>
/// 预置角色默认授权的通配规则（sys 设计 §4.5）：三段 {area}:{controller}:{action}，
/// 每段可以写 * 表示任意，如 calendar:*:index、calendar:forecast:*。只匹配三段的权限码（菜单和按钮）。
/// </summary>
public static class PermCodePattern
{
    public static bool IsMatch(string pattern, string permCode)
    {
        var patternParts = pattern.Split(':');
        var codeParts = permCode.Split(':');
        if (patternParts.Length != 3 || codeParts.Length != 3)
        {
            return false;
        }

        for (var i = 0; i < 3; i++)
        {
            if (patternParts[i] != "*" && !string.Equals(patternParts[i], codeParts[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
