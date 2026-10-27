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

/// <summary>一次运行解析出的记录；记录的类型由解析器和对应模块约定</summary>
public sealed record IngestRequest(long RunId, long TaskId, long SourceId, IReadOnlyList<object> Records);

/// <summary>入库结果：新增、更新、跳过（如内容没变）的条数，以及入库失败的条数（大于 0 时运行记为部分成功）</summary>
public sealed record IngestResult(int Inserted, int Updated, int Skipped, int Failed);
