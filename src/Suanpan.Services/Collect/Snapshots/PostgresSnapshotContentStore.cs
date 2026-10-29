using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Suanpan.Data;

namespace Suanpan.Services.Collect.Snapshots;

/// <summary>
/// 快照内容存 PostgreSQL（col_snapshot_content，DEC-023）：gzip 压缩后按内容哈希存一份。
/// 与快照记录用同一个 DbContext，在同一个事务中写入
/// </summary>
public sealed class PostgresSnapshotContentStore(SuanpanDbContext db, TimeProvider timeProvider) : ISnapshotContentStore
{
    /// <summary>
    /// 清理时，内容至少这么久没有被使用才删除：另一个事务可能正要写入引用这份内容的快照（采集设计 §4.6）
    /// </summary>
    public static readonly TimeSpan OrphanGracePeriod = TimeSpan.FromDays(1);

    public async Task PutAsync(string contentHash, byte[] content, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // 已经有这份内容时只记下使用时间；更新会锁住这一行，清理要等这个事务结束后重新判断使用时间
        var touched = await db.SnapshotContents.Where(c => c.ContentHash == contentHash)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.LastUsedAt, now), ct);
        if (touched > 0)
        {
            return;
        }

        var body = Compress(content);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO col_snapshot_content (content_hash, body, last_used_at) VALUES ({contentHash}, {body}, {now})
            ON CONFLICT (content_hash) DO UPDATE SET last_used_at = EXCLUDED.last_used_at
            """, ct);
    }

    public async Task<byte[]?> GetAsync(string contentHash, CancellationToken ct = default)
    {
        var body = await db.SnapshotContents.AsNoTracking()
            .Where(c => c.ContentHash == contentHash)
            .Select(c => c.Body)
            .FirstOrDefaultAsync(ct);
        return body is null ? null : Decompress(body);
    }

    public async Task<int> DeleteOrphansAsync(int batchSize, CancellationToken ct = default)
    {
        var cutoff = timeProvider.GetUtcNow().UtcDateTime - OrphanGracePeriod;
        var batch = db.SnapshotContents
            .Where(c => c.LastUsedAt < cutoff && !db.Snapshots.Any(s => s.ContentHash == c.ContentHash))
            .Select(c => c.ContentHash)
            .Take(batchSize);

        // 外层再判断一次使用时间：删除时遇到被并发写入锁住的行，会按更新后的使用时间重新判断，不会删掉刚被用到的内容
        return await db.SnapshotContents.Where(c => batch.Contains(c.ContentHash) && c.LastUsedAt < cutoff).ExecuteDeleteAsync(ct);
    }

    private static byte[] Compress(byte[] content)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
        {
            gzip.Write(content);
        }

        return output.ToArray();
    }

    private static byte[] Decompress(byte[] body)
    {
        using var gzip = new GZipStream(new MemoryStream(body), CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }
}
