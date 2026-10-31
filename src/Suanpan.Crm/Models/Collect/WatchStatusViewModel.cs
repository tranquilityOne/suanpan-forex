using Suanpan.Services.Collect.Watch.Models;

namespace Suanpan.Crm.Models.Collect;

/// <summary>盯盘状态页（采集设计 §8.1）：正在盯盘、接下来 24 小时的计划（含不能盯盘的事件）、最近 24 小时结束的盯盘</summary>
public sealed class WatchStatusViewModel
{
    public required WatchSettings Settings { get; init; }

    public required IReadOnlyList<WatchRunItem> Active { get; init; }

    /// <summary>计划中的各路和不能盯盘的事件，按排期时间排列；已经在盯的那一路不再列出</summary>
    public required IReadOnlyList<WatchPlanRow> Plan { get; init; }

    public required IReadOnlyList<WatchRunItem> Recent { get; init; }

    public static WatchStatusViewModel Create(WatchPlan plan, IReadOnlyList<WatchRunItem> active, IReadOnlyList<WatchRunItem> recent)
    {
        var watching = active.Select(r => (r.TaskId, r.ScheduledAt)).ToHashSet();
        return new WatchStatusViewModel
        {
            Settings = plan.Settings,
            Active = active,
            Plan = plan.Groups.Where(g => !watching.Contains((g.TaskId, g.ScheduledAt)))
                .Select(g => new WatchPlanRow(g.ScheduledAt, g, null))
                .Concat(plan.Problems.Select(p => new WatchPlanRow(p.ScheduledAt, null, p)))
                .OrderBy(r => r.ScheduledAt)
                .ToList(),
            Recent = recent,
        };
    }
}

/// <summary>盯盘计划的一行：一路盯盘，或一个不能盯盘的事件（标红并写原因）</summary>
public sealed record WatchPlanRow(DateTime ScheduledAt, WatchGroup? Group, WatchProblem? Problem);
