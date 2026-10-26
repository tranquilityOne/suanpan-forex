using Suanpan.Core.Collect.Enums;
using Suanpan.Services.Collect.Tasks.Models;

namespace Suanpan.Crm.Models.Collect;

/// <summary>采集任务列表的筛选条件（采集设计 §8.1）</summary>
public sealed class CollectTaskFilter
{
    public long? SourceId { get; set; }

    public CollectTargetType? TargetType { get; set; }

    public bool? IsEnabled { get; set; }

    public CollectTaskQuery ToQuery(int page = 1) => new(SourceId, TargetType, IsEnabled, page);
}
