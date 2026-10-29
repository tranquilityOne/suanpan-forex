using System.Collections.Concurrent;
using Suanpan.Services.Collect.Snapshots;
using Suanpan.Services.Collect.Snapshots.Models;

namespace Suanpan.Collector.Tests.Fixtures;

/// <summary>要让快照写入失败的数据源（ERR-COL-007 的测试）；整个 Collector 共用一份</summary>
public sealed class SnapshotFaults
{
    private readonly ConcurrentDictionary<long, byte> _failingSources = new();

    public void FailSource(long sourceId) => _failingSources[sourceId] = 0;

    public bool ShouldFail(long sourceId) => _failingSources.ContainsKey(sourceId);
}

/// <summary>包装真实的快照服务：<see cref="SnapshotFaults"/> 中登记的数据源保存快照时抛出异常，其余照常</summary>
public sealed class FaultySnapshotService(SnapshotService inner, SnapshotFaults faults) : ISnapshotService
{
    public Task<IReadOnlyList<long?>> SaveAsync(SnapshotBatch batch, bool skipUnchanged, CancellationToken ct = default)
        => faults.ShouldFail(batch.SourceId)
            ? throw new InvalidOperationException("模拟的快照写入失败")
            : inner.SaveAsync(batch, skipUnchanged, ct);

    public Task MarkInvalidAsync(IReadOnlyCollection<long> snapshotIds, string reason, CancellationToken ct = default)
        => inner.MarkInvalidAsync(snapshotIds, reason, ct);

    public Task MarkReferencedAsync(IReadOnlyCollection<long> snapshotIds, CancellationToken ct = default)
        => inner.MarkReferencedAsync(snapshotIds, ct);
}
