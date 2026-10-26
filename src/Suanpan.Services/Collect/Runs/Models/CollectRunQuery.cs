using Suanpan.Core.Collect.Enums;

namespace Suanpan.Services.Collect.Runs.Models;

/// <summary>运行记录的筛选条件（采集设计 §8.1，FR-002 验收第 5 条）。时间按触发时间（queued_at）筛选：Begin 含、End 不含，都是 UTC</summary>
public sealed record CollectRunQuery(
    long? TaskId = null,
    long? SourceId = null,
    CollectRunStatus? Status = null,
    CollectRunTrigger? Trigger = null,
    DateTime? Begin = null,
    DateTime? End = null,
    int Page = 1,
    int PageSize = 20);
