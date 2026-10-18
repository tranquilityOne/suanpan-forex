namespace Suanpan.Core.Sys.Rules;

/// <summary>
/// 登录失败计数与锁定（sys 设计 §4.3，BR-140）：密码错误和 TOTP 错误共用一个计数；加 1 后达到上限时锁定一段时间；
/// 锁定期间密码正确也拒绝；锁定期满后第一次失败从 1 重新计数；登录成功或管理员解锁时清零。
/// </summary>
public static class LockoutRule
{
    /// <summary>锁定截止时间晚于当前时间即为锁定</summary>
    public static bool IsLocked(DateTime? lockedUntil, DateTime now) => lockedUntil > now;

    /// <summary>剩余锁定时间（分钟，向上取整，至少 1），用于提示</summary>
    public static int RemainingMinutes(DateTime lockedUntil, DateTime now)
        => Math.Max(1, (int)Math.Ceiling((lockedUntil - now).TotalMinutes));

    /// <summary>记一次失败后的计数和锁定截止时间</summary>
    public static LockoutState AfterFailure(int failCount, DateTime? lockedUntil, DateTime now, int maxFailures, TimeSpan lockDuration)
    {
        var count = lockedUntil is { } until && until <= now ? 1 : failCount + 1;
        return count >= Math.Max(1, maxFailures)
            ? new LockoutState(count, now + lockDuration)
            : new LockoutState(count, null);
    }
}

/// <summary>失败次数和锁定截止时间（UTC）；LockedUntil 为空表示未锁定</summary>
public readonly record struct LockoutState(int FailCount, DateTime? LockedUntil);
