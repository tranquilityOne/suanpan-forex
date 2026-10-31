using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Collect.Entities;
using Suanpan.Data;
using Suanpan.Services.Collect.Snapshots.Models;
using Suanpan.Services.Common.Querying;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Collect.Snapshots;

public sealed class SnapshotService(SuanpanDbContext db, ISnapshotContentStore contentStore) : ISnapshotService
{
    private const int MaxUrlLength = 2048;

    private const int MaxRequestKeyLength = 512;

    private const int MaxContentTypeLength = 128;

    private const int MaxInvalidReasonLength = 500;

    public Task<PagedResult<SnapshotRow>> QueryAsync(SnapshotQuery query, CancellationToken ct = default)
        => (from s in db.Snapshots.AsNoTracking().Filter(db, query)
            join source in db.DataSources on s.SourceId equals source.Id into sources
            from source in sources.DefaultIfEmpty()
            join task in db.CollectTasks on s.TaskId equals task.Id into tasks
            from task in tasks.DefaultIfEmpty()
            join run in db.CollectRuns on s.RunId equals run.Id into runs
            from run in runs.DefaultIfEmpty()
            orderby s.Id descending
            select new SnapshotRow(
                s.Id, s.FetchedAt, s.SourceId, source == null ? null : source.Name, s.TaskId, task == null ? null : task.Name,
                s.Url, s.HttpStatus, s.Size, s.Unchanged, s.IsValid, s.RunId, s.Attempt, s.Seq, run == null ? null : run.Status))
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

    public async Task<SnapshotDetail?> GetAsync(long id, CancellationToken ct = default)
    {
        var item = await (
            from s in db.Snapshots.AsNoTracking()
            where s.Id == id
            join source in db.DataSources on s.SourceId equals source.Id into sources
            from source in sources.DefaultIfEmpty()
            join task in db.CollectTasks on s.TaskId equals task.Id into tasks
            from task in tasks.DefaultIfEmpty()
            select new { Snapshot = s, SourceName = source == null ? null : source.Name, TaskName = task == null ? null : task.Name })
            .FirstOrDefaultAsync(ct);
        if (item is null)
        {
            return null;
        }

        var run = await db.CollectRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == item.Snapshot.RunId, ct);
        var content = await contentStore.GetAsync(item.Snapshot.ContentHash, ct);
        return new SnapshotDetail(item.Snapshot, item.SourceName, item.TaskName, run, content);
    }

    public async Task<IReadOnlyList<long>> SaveAsync(SnapshotBatch batch, bool skipUnchanged, CancellationToken ct = default)
    {
        var added = new List<(int Index, Snapshot Snapshot)>();

        // 盯盘时没写记录的响应 → 上一个相同内容的快照（库中已有的 id，或同一批中刚加的快照）
        var reused = new List<(int Index, long? Id, Snapshot? Added)>();

        // 同一批中出现过的请求标识 → 最近的快照：同一次请求里重复请求同一个地址时，后一个与前一个比较
        var latest = new Dictionary<string, (string Hash, long? Id, Snapshot? Added)>(StringComparer.Ordinal);
        try
        {
            await db.InTransactionAsync(async () =>
            {
                for (var i = 0; i < batch.Responses.Count; i++)
                {
                    var response = batch.Responses[i];
                    var requestKey = Truncate(response.RequestKey, MaxRequestKeyLength);
                    var hash = Convert.ToHexStringLower(SHA256.HashData(response.Body));
                    (string Hash, long? Id, Snapshot? Added)? previous = latest.TryGetValue(requestKey, out var seen)
                        ? seen
                        : await db.Snapshots.AsNoTracking()
                            .Where(s => s.SourceId == batch.SourceId && s.RequestKey == requestKey)
                            .OrderByDescending(s => s.Id)
                            .Select(s => new { s.ContentHash, s.Id })
                            .FirstOrDefaultAsync(ct) is { } stored
                            ? (stored.ContentHash, stored.Id, null)
                            : null;

                    var unchanged = previous?.Hash == hash;
                    if (unchanged && skipUnchanged)
                    {
                        reused.Add((i, previous!.Value.Id, previous.Value.Added));
                        continue;
                    }

                    await contentStore.PutAsync(hash, response.Body, ct);
                    var snapshot = new Snapshot
                    {
                        RunId = batch.RunId,
                        Attempt = batch.Attempt,
                        Seq = (short)i,
                        SourceId = batch.SourceId,
                        TaskId = batch.TaskId,
                        Url = Truncate(response.Url, MaxUrlLength),
                        RequestKey = requestKey,
                        FetchedAt = response.FetchedAt,
                        HttpStatus = (short)response.HttpStatus,
                        ContentType = response.ContentType is { } contentType ? Truncate(contentType, MaxContentTypeLength) : null,
                        ContentHash = hash,
                        Size = response.Body.Length,
                        Unchanged = unchanged,
                    };
                    db.Snapshots.Add(snapshot);
                    added.Add((i, snapshot));
                    latest[requestKey] = (hash, null, snapshot);
                }

                await db.SaveChangesAsync(ct);
            }, ct);

            var ids = new long[batch.Responses.Count];
            foreach (var (index, snapshot) in added)
            {
                ids[index] = snapshot.Id;
            }

            foreach (var (index, id, addedSnapshot) in reused)
            {
                ids[index] = id ?? addedSnapshot!.Id;
            }

            return ids;
        }
        finally
        {
            // 调用方（执行器）在同一个 DbContext 中继续改运行记录，不留下跟踪的快照
            foreach (var (_, snapshot) in added)
            {
                db.Entry(snapshot).State = EntityState.Detached;
            }
        }
    }

    public async Task MarkInvalidAsync(IReadOnlyCollection<long> snapshotIds, string reason, CancellationToken ct = default)
    {
        var invalidReason = Truncate(reason, MaxInvalidReasonLength);
        await db.Snapshots.Where(s => snapshotIds.Contains(s.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsValid, false).SetProperty(x => x.InvalidReason, invalidReason), ct);
    }

    public async Task MarkReferencedAsync(IReadOnlyCollection<long> snapshotIds, CancellationToken ct = default)
    {
        await db.Snapshots.Where(s => snapshotIds.Contains(s.Id) && !s.Referenced)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Referenced, true), ct);
    }

    private static string Truncate(string value, int maxLength) => value.Length > maxLength ? value[..maxLength] : value;
}
