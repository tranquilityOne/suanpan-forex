namespace Suanpan.Core.Collect.Enums;

/// <summary>运行记录的触发方式（PRD §8.1.2）</summary>
public enum CollectRunTrigger : short
{
    /// <summary>定时：Job 按 cron 分发</summary>
    Scheduled = 1,

    /// <summary>盯盘（FR-003）</summary>
    Watch = 2,

    /// <summary>手动：后台"立即执行"</summary>
    Manual = 3,

    /// <summary>重新解析（FR-004）</summary>
    Reparse = 4,
}
