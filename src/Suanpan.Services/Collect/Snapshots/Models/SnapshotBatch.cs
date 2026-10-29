namespace Suanpan.Services.Collect.Snapshots.Models;

/// <summary>运行记录一次请求的全部响应，按适配器返回的顺序（第 i 个响应的快照序号为 i）</summary>
public sealed record SnapshotBatch(long RunId, short Attempt, long TaskId, long SourceId, IReadOnlyList<SnapshotInput> Responses);
