using Suanpan.Core.Collect.Enums;

namespace Suanpan.Services.Collect.Tasks.Models;

/// <summary>采集任务列表的筛选条件（采集设计 §8.1）</summary>
public sealed record CollectTaskQuery(
    long? SourceId = null,
    CollectTargetType? TargetType = null,
    bool? IsEnabled = null,
    int Page = 1,
    int PageSize = 20);
