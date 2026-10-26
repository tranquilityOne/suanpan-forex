using Suanpan.Core.Collect.Enums;
using Suanpan.Crm.Mvc;
using Suanpan.Services.Collect.Runs.Models;

namespace Suanpan.Crm.Models.Collect;

/// <summary>运行记录的筛选条件（采集设计 §8.1，FR-002 验收第 5 条）。日期按操作者时区选，包含两端，按触发时间筛选</summary>
public sealed class CollectRunFilter
{
    public long? TaskId { get; set; }

    public long? SourceId { get; set; }

    public CollectRunStatus? Status { get; set; }

    public CollectRunTrigger? Trigger { get; set; }

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public CollectRunQuery ToQuery(TimeZoneInfo timeZone, int page = 1)
    {
        var (begin, end) = DateRangeFilter.ToUtc(From, To, timeZone);
        return new CollectRunQuery(TaskId, SourceId, Status, Trigger, begin, end, page);
    }
}
