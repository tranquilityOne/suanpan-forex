using Suanpan.Core.Collect.Enums;

namespace Suanpan.Services.Collect.Tasks.Models;

/// <summary>采集任务列表的一行（采集设计 §8.1）；时间都是 UTC</summary>
public sealed record CollectTaskRow(
    long Id,
    string Name,
    long SourceId,
    string SourceName,
    bool SourceEnabled,
    string ParserKey,
    CollectTargetType TargetType,
    string? Cron,
    bool IsEnabled,
    CollectRunStatus? LastRunStatus,
    DateTime? LastRunAt)
{
    /// <summary>解析器的中文名；解析器已不在目录中时为空</summary>
    public string? ParserName { get; init; }

    /// <summary>下次定时执行的时间；任务或数据源停用、没有 cron 时为空</summary>
    public DateTime? NextRunAt { get; init; }
}
