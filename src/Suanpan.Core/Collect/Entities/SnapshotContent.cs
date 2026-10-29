namespace Suanpan.Core.Collect.Entities;

/// <summary>
/// 快照内容（col_snapshot_content，采集设计 §2.1、§5.5，DEC-023）：按内容哈希去重，同样的内容只存一份（BR-021、FR-004 验收第 2 条）。
/// 读写经 ISnapshotContentStore，以后换成对象存储只换实现
/// </summary>
public class SnapshotContent
{
    /// <summary>原始内容的 SHA-256，十六进制小写</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>gzip 压缩后的原始内容</summary>
    public byte[] Body { get; set; } = [];

    /// <summary>最近一次被快照使用的时间；清理只删除没有快照引用、且 1 天内没有被使用的内容，避免删掉正在写入的快照要用的内容</summary>
    public DateTime LastUsedAt { get; set; }
}
