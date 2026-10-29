using Suanpan.Services.Calendar.Unmatched.Models;
using Suanpan.Shared.Mvc;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Calendar.Unmatched;

/// <summary>
/// 未匹配条目的处理（ERR-EVT-003、BR-156，日历设计 §4.5）：编辑按发布标识忽略、恢复，新增映射后立即重新匹配
/// </summary>
public interface IUnmatchedEntryService
{
    /// <summary>按"数据源 + 发布标识"分组，最近要发布的组排在前面；每组内按参考期排序</summary>
    Task<PagedResult<UnmatchedReleaseGroup>> QueryAsync(UnmatchedEntryQuery query, CancellationToken ct = default);

    /// <summary>已忽略的发布，按数据源、发布标识排序</summary>
    Task<PagedResult<IgnoredReleaseRow>> QueryIgnoredAsync(IgnoredReleaseQuery query, CancellationToken ct = default);

    /// <summary>忽略：写入已忽略的发布并删除这个标识的未匹配条目；以后同一标识的条目不再写入。已忽略的再次忽略视为成功</summary>
    Task<ManageResult> IgnoreAsync(long sourceId, string releaseKey, CancellationToken ct = default);

    /// <summary>恢复：删除忽略记录，下一次同步时条目重新出现</summary>
    Task<ManageResult> RestoreAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// 重新匹配：按当前的排期映射，把已有映射的条目交给 ScheduleApplier 建成或更新事件，然后删除这些条目（与排期入库的清理一致）；
    /// 已经过去的、参考期不符合指标频率的不建事件。返回匹配上启用指标的条目数
    /// </summary>
    Task<int> RematchAsync(CancellationToken ct = default);
}
