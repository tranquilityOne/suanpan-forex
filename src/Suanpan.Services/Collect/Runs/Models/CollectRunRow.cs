using Suanpan.Core.Collect.Enums;

namespace Suanpan.Services.Collect.Runs.Models;

/// <summary>运行记录列表的一行（采集设计 §8.1）；时间都是 UTC</summary>
public sealed record CollectRunRow(
    long Id,
    long TaskId,
    string TaskName,
    CollectRunTrigger Trigger,
    string? TriggeredBy,
    CollectRunStatus Status,
    DateTime QueuedAt,
    DateTime? StartedAt,
    DateTime? EndedAt,
    short AttemptCount,
    int InsertedCount,
    int UpdatedCount,
    int SkippedCount);
