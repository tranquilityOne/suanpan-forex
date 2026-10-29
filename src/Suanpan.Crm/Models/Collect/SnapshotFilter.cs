using Suanpan.Core.Collect.Enums;
using Suanpan.Crm.Mvc;
using Suanpan.Services.Collect.Snapshots.Models;

namespace Suanpan.Crm.Models.Collect;

/// <summary>
/// 快照的筛选条件（采集设计 §8.1，FR-004 主流程第 1 步）。日期按操作者时区选，包含两端，按请求时间筛选。
/// 选定任务后才能重新解析（按任务进行，§5.6）
/// </summary>
public sealed class SnapshotFilter
{
    public long? SourceId { get; set; }

    public long? TaskId { get; set; }

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    /// <summary>所属运行记录的状态</summary>
    public CollectRunStatus? RunStatus { get; set; }

    public bool? IsValid { get; set; }

    public SnapshotQuery ToQuery(TimeZoneInfo timeZone, int page = 1)
    {
        var (begin, end) = DateRangeFilter.ToUtc(From, To, timeZone);
        return new SnapshotQuery(SourceId, TaskId, RunId: null, begin, end, RunStatus, IsValid, page);
    }

    /// <summary>作为查询串或隐藏字段带到重新解析的页面，与列表看到的范围一致；prefix 为表单模型中的属性名前缀。没有值的条件不带</summary>
    public Dictionary<string, string> ToRouteValues(string prefix = "")
    {
        var values = new (string Name, string? Value)[]
        {
            (nameof(SourceId), SourceId?.ToString()),
            (nameof(TaskId), TaskId?.ToString()),
            (nameof(From), From?.ToString("yyyy-MM-dd")),
            (nameof(To), To?.ToString("yyyy-MM-dd")),
            (nameof(RunStatus), RunStatus?.ToString()),
            (nameof(IsValid), IsValid?.ToString().ToLowerInvariant()),
        };
        return values.Where(v => v.Value is not null).ToDictionary(v => prefix + v.Name, v => v.Value!);
    }
}
