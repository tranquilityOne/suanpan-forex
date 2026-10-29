using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;

namespace Suanpan.Services.Collect.Runs.Models;

/// <summary>运行详情（采集设计 §8.1）：运行记录本身，加上任务和数据源的名称、这次运行保存的快照</summary>
/// <param name="SourceName">数据源已删除时为空（运行记录保留 90 天，任务改换数据源后旧数据源可以删除）</param>
/// <param name="Snapshots">按请求次数、序号排列；重新解析不产生快照，处理的快照记在 Run.Reparse 中</param>
public sealed record CollectRunDetail(
    CollectRun Run, string TaskName, CollectTargetType TargetType, string? SourceName, IReadOnlyList<RunSnapshot> Snapshots);

/// <summary>运行详情中列出的一个快照</summary>
public sealed record RunSnapshot(long Id, short Attempt, short Seq, string Url, short HttpStatus, int Size, bool Unchanged, bool IsValid);
