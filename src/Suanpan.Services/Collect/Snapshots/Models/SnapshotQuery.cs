using Suanpan.Core.Collect.Enums;

namespace Suanpan.Services.Collect.Snapshots.Models;

/// <summary>
/// 快照的筛选条件（采集设计 §8.1，FR-004 主流程第 1 步）：数据源、任务、运行记录、请求时间（Begin 含、End 不含，UTC）、
/// 所属运行记录的状态（运行记录已清理的快照不再匹配）、是否有效
/// </summary>
public sealed record SnapshotQuery(
    long? SourceId = null,
    long? TaskId = null,
    long? RunId = null,
    DateTime? Begin = null,
    DateTime? End = null,
    CollectRunStatus? RunStatus = null,
    bool? IsValid = null,
    int Page = 1,
    int PageSize = 20);
