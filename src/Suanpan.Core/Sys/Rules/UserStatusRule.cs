namespace Suanpan.Core.Sys.Rules;

/// <summary>
/// 账号停用和管理员角色的约束（sys 设计 §4.1，BR-144）：不能停用自己；系统里至少保留一个未停用的管理员。
/// "未停用"包括待激活：初始管理员在激活前也必须受保护。
/// </summary>
public static class UserStatusRule
{
    /// <summary>能否停用账号；返回 null 表示可以，否则返回原因</summary>
    /// <param name="targetUserId">要停用的账号</param>
    /// <param name="currentUserId">当前操作人</param>
    /// <param name="targetIsActiveAdmin">要停用的账号是否为未停用的管理员</param>
    /// <param name="activeAdminCount">系统中未停用的管理员数量（含要停用的账号）</param>
    public static string? CanDisable(long targetUserId, long? currentUserId, bool targetIsActiveAdmin, int activeAdminCount)
    {
        if (currentUserId == targetUserId)
        {
            return "不能停用自己";
        }

        return LastAdminError(targetIsActiveAdmin, activeAdminCount, "不能停用最后一个管理员");
    }

    /// <summary>能否去掉账号的管理员角色；返回 null 表示可以，否则返回原因</summary>
    public static string? CanRemoveAdminRole(bool targetIsActiveAdmin, int activeAdminCount)
        => LastAdminError(targetIsActiveAdmin, activeAdminCount, "不能去掉最后一个管理员的管理员角色");

    private static string? LastAdminError(bool targetIsActiveAdmin, int activeAdminCount, string message)
        => targetIsActiveAdmin && activeAdminCount <= 1 ? message : null;
}
