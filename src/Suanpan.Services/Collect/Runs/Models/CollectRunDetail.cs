using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;

namespace Suanpan.Services.Collect.Runs.Models;

/// <summary>运行详情（采集设计 §8.1）：运行记录本身，加上任务和数据源的名称</summary>
/// <param name="SourceName">数据源已删除时为空（运行记录保留 90 天，任务改换数据源后旧数据源可以删除）</param>
public sealed record CollectRunDetail(CollectRun Run, string TaskName, CollectTargetType TargetType, string? SourceName);
