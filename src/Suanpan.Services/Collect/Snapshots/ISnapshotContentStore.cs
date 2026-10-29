namespace Suanpan.Services.Collect.Snapshots;

/// <summary>
/// 快照内容的存储（采集设计 §5.5，DEC-023）：按内容的 SHA-256 存取，同样的内容只存一份。
/// 第一阶段存 PostgreSQL（<see cref="PostgresSnapshotContentStore"/>）；以后量大时换成对象存储，只换这个实现，快照记录仍在数据库中
/// </summary>
public interface ISnapshotContentStore
{
    /// <summary>保存内容；已经有这个哈希时只记下使用时间，不重复存储（BR-021）</summary>
    Task PutAsync(string contentHash, byte[] content, CancellationToken ct = default);

    /// <summary>取回原始内容；没有时返回 null</summary>
    Task<byte[]?> GetAsync(string contentHash, CancellationToken ct = default);

    /// <summary>删除最多 batchSize 条不再被快照引用的内容，返回删除的条数（采集设计 §4.6）</summary>
    Task<int> DeleteOrphansAsync(int batchSize, CancellationToken ct = default);
}
