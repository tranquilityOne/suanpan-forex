namespace Suanpan.Core.Collect.Entities;

/// <summary>
/// 重新解析的参数（col_run.reparse，jsonb，采集设计 §5.6）：用哪个解析器、处理哪些快照。
/// 只有触发方式为"重新解析"的运行记录有；键名与属性名相同
/// </summary>
public class CollectRunReparse
{
    /// <summary>解析器标识：默认是任务的解析器，可以改选同一适配器下的其他解析器（FR-004 主流程第 3 步）</summary>
    public string ParserKey { get; set; } = string.Empty;

    /// <summary>选中的快照，按 id 升序；最多 5000 个（BR-024）</summary>
    public List<long> SnapshotIds { get; set; } = [];
}
