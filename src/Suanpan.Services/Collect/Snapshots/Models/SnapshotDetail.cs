using Suanpan.Core.Collect.Entities;

namespace Suanpan.Services.Collect.Snapshots.Models;

/// <summary>
/// 快照详情（采集设计 §5.5、§8.1）：快照记录、原始内容（内容已被清理时为空），以及所属运行记录"当时的解析结果"
/// </summary>
/// <param name="Run">所属运行记录；过了 90 天的保留期被清理后为空，页面显示"运行记录已清理"</param>
public sealed record SnapshotDetail(Snapshot Snapshot, string? SourceName, string? TaskName, CollectRun? Run, byte[]? Content);
