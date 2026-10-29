namespace Suanpan.Core.Collect.Entities;

/// <summary>
/// 快照（col_snapshot，采集设计 §2.1、§5.5）：一次请求的一个响应，先存快照再解析（硬性规则 14）。系统写入，不带审计字段；
/// 内容按哈希存在 <see cref="SnapshotContent"/> 中，同样的内容只存一份（BR-021）。保留期见采集设计 §4.6（BR-022）
/// </summary>
public class Snapshot
{
    public long Id { get; set; }

    /// <summary>产生它的运行记录（运行记录保留 90 天，过期后不再能关联到记录）</summary>
    public long RunId { get; set; }

    /// <summary>运行记录的第几次请求</summary>
    public short Attempt { get; set; }

    /// <summary>这次请求的第几个响应，从 0 开始，与 FetchResult.Responses 的顺序相同</summary>
    public short Seq { get; set; }

    /// <summary>冗余，运行记录删除后仍能按数据源筛选</summary>
    public long SourceId { get; set; }

    /// <summary>冗余，运行记录删除后仍能按任务筛选</summary>
    public long TaskId { get; set; }

    /// <summary>请求地址，不含查询串（有的数据源把凭据放在查询串里）</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>请求标识，判断"未变化"用（BR-021）；默认等于 <see cref="Url"/>，不含凭据</summary>
    public string RequestKey { get; set; } = string.Empty;

    /// <summary>收到响应的时间</summary>
    public DateTime FetchedAt { get; set; }

    public short HttpStatus { get; set; }

    public string? ContentType { get; set; }

    /// <summary>原始内容的 SHA-256，十六进制小写；对应 <see cref="SnapshotContent.ContentHash"/>（BR-020 的"存储位置"）</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>原始内容的字节数</summary>
    public int Size { get; set; }

    /// <summary>与同一数据源、同一请求标识的上一个快照内容相同（BR-021 的"未变化"）</summary>
    public bool Unchanged { get; set; }

    /// <summary>内容为空或被判定为拦截页时为否（ERR-PARSE-003），不参与重新解析</summary>
    public bool IsValid { get; set; } = true;

    public string? InvalidReason { get; set; }

    /// <summary>被入库数据引用（BR-022）：入库服务写入引用这个快照的数据时置为是，不再改回；被引用的快照保留更久</summary>
    public bool Referenced { get; set; }
}
