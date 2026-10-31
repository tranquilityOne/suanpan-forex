using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Reference.Entities;
using Suanpan.Core.Reference.Enums;
using Suanpan.Data;
using Suanpan.Services.Collect.Watch.Models;

namespace Suanpan.Services.Collect.Watch;

/// <summary>
/// 按指标找盯盘用的数据源和公布值任务（采集设计 §5.7，§11 第 23、24 项）：取"公布值"映射中优先级最高、已启用的数据源，
/// 再取这个数据源已启用、解析器与映射相同的公布值任务（多个时取 id 最小的）。盯盘计划和延迟后的补采共用
/// </summary>
internal sealed class WatchTargets
{
    private readonly ILookup<string, (IndicatorSource Mapping, DataSource Source)> _mappings;
    private readonly IReadOnlyList<CollectTask> _tasks;

    private WatchTargets(ILookup<string, (IndicatorSource Mapping, DataSource Source)> mappings, IReadOnlyList<CollectTask> tasks)
        => (_mappings, _tasks) = (mappings, tasks);

    public static async Task<WatchTargets> LoadAsync(SuanpanDbContext db, IReadOnlyCollection<string> indicatorCodes, CancellationToken ct)
    {
        var mappings = await (
                from m in db.IndicatorSources.AsNoTracking()
                where m.Purpose == IndicatorSourcePurpose.Release && indicatorCodes.Contains(m.IndicatorCode)
                join s in db.DataSources on m.SourceId equals s.Id
                select new { Mapping = m, Source = s })
            .ToListAsync(ct);
        var sourceIds = mappings.Select(x => x.Source.Id).Distinct().ToList();
        var tasks = await db.CollectTasks.AsNoTracking()
            .Where(t => sourceIds.Contains(t.SourceId) && t.TargetType == CollectTargetType.Release && t.IsEnabled)
            .OrderBy(t => t.Id)
            .ToListAsync(ct);
        return new WatchTargets(mappings.ToLookup(x => x.Mapping.IndicatorCode, x => (x.Mapping, x.Source)), tasks);
    }

    public WatchTarget Resolve(string indicatorCode)
    {
        var candidates = _mappings[indicatorCode].OrderBy(x => x.Mapping.Priority).ToList();
        if (candidates.Count == 0)
        {
            return WatchTarget.Problem(WatchProblemKind.NoMapping, "指标没有\"公布值\"映射");
        }

        if (candidates.FirstOrDefault(x => x.Source.IsEnabled) is not { Source: not null } chosen)
        {
            return WatchTarget.Problem(WatchProblemKind.SourceDisabled,
                $"映射的数据源已停用：{string.Join("、", candidates.Select(x => x.Source.Name).Distinct())}");
        }

        return _tasks.FirstOrDefault(t => t.SourceId == chosen.Source.Id && t.ParserKey == chosen.Mapping.ParserKey) is { } task
            ? new WatchTarget(chosen.Mapping, chosen.Source, task, null, null)
            : WatchTarget.Problem(WatchProblemKind.NoTask,
                $"数据源 {chosen.Source.Name} 没有已启用的公布值采集任务（解析器 {chosen.Mapping.ParserKey}）");
    }
}

/// <summary>找到的映射、数据源和任务；找不到时 Kind 和 Reason 说明原因</summary>
internal sealed record WatchTarget(IndicatorSource? Mapping, DataSource? Source, CollectTask? Task, WatchProblemKind? Kind, string? Reason)
{
    public static WatchTarget Problem(WatchProblemKind kind, string reason) => new(null, null, null, kind, reason);
}
