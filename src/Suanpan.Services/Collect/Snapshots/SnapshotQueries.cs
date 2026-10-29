using Suanpan.Core.Collect.Entities;
using Suanpan.Data;
using Suanpan.Services.Collect.Snapshots.Models;

namespace Suanpan.Services.Collect.Snapshots;

/// <summary>快照的筛选（采集设计 §8.1）：快照列表和重新解析"全部筛选结果"用同一套条件，选中的范围与看到的一致</summary>
public static class SnapshotQueries
{
    public static IQueryable<Snapshot> Filter(this IQueryable<Snapshot> snapshots, SuanpanDbContext db, SnapshotQuery query)
    {
        if (query.SourceId is { } sourceId)
        {
            snapshots = snapshots.Where(s => s.SourceId == sourceId);
        }

        if (query.TaskId is { } taskId)
        {
            snapshots = snapshots.Where(s => s.TaskId == taskId);
        }

        if (query.RunId is { } runId)
        {
            snapshots = snapshots.Where(s => s.RunId == runId);
        }

        if (query.Begin is { } begin)
        {
            snapshots = snapshots.Where(s => s.FetchedAt >= begin);
        }

        if (query.End is { } end)
        {
            snapshots = snapshots.Where(s => s.FetchedAt < end);
        }

        if (query.RunStatus is { } status)
        {
            snapshots = snapshots.Where(s => db.CollectRuns.Any(r => r.Id == s.RunId && r.Status == status));
        }

        if (query.IsValid is { } isValid)
        {
            snapshots = snapshots.Where(s => s.IsValid == isValid);
        }

        return snapshots;
    }
}
