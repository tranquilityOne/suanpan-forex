namespace Suanpan.Core.Calendar.Enums;

/// <summary>锁定冲突的处理状态（BR-062）</summary>
public enum LockConflictStatus : short
{
    /// <summary>待处理</summary>
    Pending = 1,

    /// <summary>已处理</summary>
    Resolved = 2,
}
