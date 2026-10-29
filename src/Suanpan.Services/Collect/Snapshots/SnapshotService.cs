using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Collect.Entities;
using Suanpan.Data;
using Suanpan.Services.Collect.Snapshots.Models;

namespace Suanpan.Services.Collect.Snapshots;

public sealed class SnapshotService(SuanpanDbContext db, ISnapshotContentStore contentStore) : ISnapshotService
{
    private const int MaxUrlLength = 2048;

    private const int MaxRequestKeyLength = 512;

    private const int MaxContentTypeLength = 128;

    private const int MaxInvalidReasonLength = 500;

    public async Task<IReadOnlyList<long?>> SaveAsync(SnapshotBatch batch, bool skipUnchanged, CancellationToken ct = default)
    {
        var added = new List<(int Index, Snapshot Snapshot)>();

        // 同一批中出现过的请求标识 → 内容哈希：同一次请求里重复请求同一个地址时，后一个与前一个比较
        var latestHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            await db.InTransactionAsync(async () =>
            {
                for (var i = 0; i < batch.Responses.Count; i++)
                {
                    var response = batch.Responses[i];
                    var requestKey = Truncate(response.RequestKey, MaxRequestKeyLength);
                    var hash = Convert.ToHexStringLower(SHA256.HashData(response.Body));
                    var previous = latestHashes.TryGetValue(requestKey, out var seen)
                        ? seen
                        : await db.Snapshots.AsNoTracking()
                            .Where(s => s.SourceId == batch.SourceId && s.RequestKey == requestKey)
                            .OrderByDescending(s => s.Id)
                            .Select(s => s.ContentHash)
                            .FirstOrDefaultAsync(ct);
                    latestHashes[requestKey] = hash;

                    var unchanged = previous == hash;
                    if (unchanged && skipUnchanged)
                    {
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
                }

                await db.SaveChangesAsync(ct);
            }, ct);

            var ids = new long?[batch.Responses.Count];
            foreach (var (index, snapshot) in added)
            {
                ids[index] = snapshot.Id;
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
