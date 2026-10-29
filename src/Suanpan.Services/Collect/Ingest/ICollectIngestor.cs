using Suanpan.Core.Collect.Enums;

namespace Suanpan.Services.Collect.Ingest;

/// <summary>
/// 采集结果的入库服务（采集设计 §5.3）：每种目标数据类型一个，由负责该数据的模块在 Services/{模块}/ 中实现并注册，
/// Collector 按任务的目标数据类型调用。入库服务负责校验（FR-026）、字段锁定（FR-011）和幂等：同一批记录重复入库结果不变
/// </summary>
public interface ICollectIngestor
{
    CollectTargetType TargetType { get; }

    Task<IngestResult> IngestAsync(IngestRequest request, CancellationToken ct);
}

/// <summary>
/// 一次运行解析出的记录；记录的类型由解析器和对应模块约定。SnapshotIds 与解析器输入的响应一一对应（第 i 个响应的快照 id），
/// 记录来自哪个响应由记录类型约定，入库服务据此记下来源快照（BR-045），并调用 ISnapshotService.MarkReferencedAsync（BR-022）
/// </summary>
public sealed record IngestRequest(long RunId, long TaskId, long SourceId, IReadOnlyList<object> Records, IReadOnlyList<long> SnapshotIds);

/// <summary>
/// 入库结果：新增、更新、跳过（如内容没变）的条数，入库失败的条数（大于 0 时运行记为部分成功），
/// 以及因字段锁定没有更新的条数（BR-062，采集设计 §5.3）
/// </summary>
public sealed record IngestResult(int Inserted, int Updated, int Skipped, int Failed, int Locked = 0);
