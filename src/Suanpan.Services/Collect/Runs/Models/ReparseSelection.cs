using Suanpan.Services.Collect.Snapshots.Models;

namespace Suanpan.Services.Collect.Runs.Models;

/// <summary>
/// 重新解析选中的快照（采集设计 §5.6）：快照列表的筛选条件（必须选定任务），加上勾选的快照 id；
/// 没有勾选时为"全部筛选结果"，勾选了只取其中仍符合筛选条件的。分页参数不起作用
/// </summary>
public sealed record ReparseSelection(SnapshotQuery Filter, IReadOnlyList<long> SnapshotIds);
