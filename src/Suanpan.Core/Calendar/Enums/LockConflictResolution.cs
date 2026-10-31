namespace Suanpan.Core.Calendar.Enums;

/// <summary>编辑怎么处理锁定冲突（FR-011 主流程第 4 步）</summary>
public enum LockConflictResolution : short
{
    /// <summary>保持锁定</summary>
    KeepLocked = 1,

    /// <summary>解锁并采用采集值</summary>
    AdoptCollected = 2,

    /// <summary>在事件详情中单独解锁：下一次采集照常更新（日历设计 §3.9）</summary>
    Unlocked = 3,
}
