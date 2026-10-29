using Suanpan.Core.Collect.Enums;

namespace Suanpan.Core.Collect.Entities;

/// <summary>
/// 运行记录（col_run，采集设计 §2.1）。系统写入，只追加和更新状态，不带审计字段；保留 90 天（BR-011）。
/// 状态流转见 CollectRunStatusRule，用条件更新防止并发改写
/// </summary>
public class CollectRun
{
    public long Id { get; set; }

    public long TaskId { get; set; }

    /// <summary>冗余，用于按数据源筛选（FR-002 验收第 5 条）</summary>
    public long SourceId { get; set; }

    public CollectRunTrigger Trigger { get; set; }

    /// <summary>定时触发对应的 cron 时间点（UTC）；其他触发方式为空。同一任务同一时间点只创建一次</summary>
    public DateTime? ScheduledFor { get; set; }

    /// <summary>手动触发的操作人登录名</summary>
    public string? TriggeredBy { get; set; }

    public CollectRunStatus Status { get; set; } = CollectRunStatus.Queued;

    public DateTime QueuedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    /// <summary>已发起的请求次数（含重试）</summary>
    public short AttemptCount { get; set; }

    /// <summary>等待重试时的下一次时间</summary>
    public DateTime? NextAttemptAt { get; set; }

    public List<CollectAttempt> Attempts { get; set; } = [];

    public int InsertedCount { get; set; }

    public int UpdatedCount { get; set; }

    public int SkippedCount { get; set; }

    /// <summary>因字段锁定没有更新的条数（BR-062、FR-004 主流程第 4 步）</summary>
    public int LockedCount { get; set; }

    /// <summary>
    /// 解析器输出的记录（jsonb）：<c>{"total": 总条数, "records": [前 1000 条]}</c>，快照详情中"当时的解析结果"用（FR-004 主流程第 2 步）；
    /// 没有解析到这一步时为空
    /// </summary>
    public string? ParseOutput { get; set; }

    /// <summary>失败或跳过的错误码（采集设计 §9）</summary>
    public string? ErrorCode { get; set; }

    /// <summary>面向工程师的原因</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>贯穿下发、执行、入库的日志追踪（NFR-050）</summary>
    public string? TraceId { get; set; }
}
