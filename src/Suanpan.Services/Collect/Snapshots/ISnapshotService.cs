using Suanpan.Services.Collect.Snapshots.Models;

namespace Suanpan.Services.Collect.Snapshots;

/// <summary>快照（采集设计 §5.5，FR-004）：保存采集到的响应，标记无效和被引用的快照</summary>
public interface ISnapshotService
{
    /// <summary>
    /// 在一个事务中保存一次请求的全部响应，返回各响应的快照 id（顺序与 batch.Responses 相同）。
    /// 内容按哈希只存一份；与同一数据源、同一请求标识的上一个快照内容相同时记为"未变化"（BR-021）。
    /// skipUnchanged 为 true（盯盘，BR-016）时未变化的响应不写快照记录，对应位置返回 null
    /// </summary>
    Task<IReadOnlyList<long?>> SaveAsync(SnapshotBatch batch, bool skipUnchanged, CancellationToken ct = default);

    /// <summary>内容为空或被判定为拦截页（ERR-PARSE-003）：标记为无效，不参与重新解析</summary>
    Task MarkInvalidAsync(IReadOnlyCollection<long> snapshotIds, string reason, CancellationToken ct = default);

    /// <summary>入库服务写入引用这些快照的数据时调用，被引用的快照保留更久（BR-022，采集设计 §4.6）</summary>
    Task MarkReferencedAsync(IReadOnlyCollection<long> snapshotIds, CancellationToken ct = default);
}
