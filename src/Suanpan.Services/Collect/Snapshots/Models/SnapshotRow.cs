using Suanpan.Core.Collect.Enums;

namespace Suanpan.Services.Collect.Snapshots.Models;

/// <summary>快照列表的一行（采集设计 §8.1）；数据源、任务已删除或运行记录已清理时对应的名称、状态为空</summary>
public sealed record SnapshotRow(
    long Id,
    DateTime FetchedAt,
    long SourceId,
    string? SourceName,
    long TaskId,
    string? TaskName,
    string Url,
    short HttpStatus,
    int Size,
    bool Unchanged,
    bool IsValid,
    long RunId,
    short Attempt,
    short Seq,
    CollectRunStatus? RunStatus);
