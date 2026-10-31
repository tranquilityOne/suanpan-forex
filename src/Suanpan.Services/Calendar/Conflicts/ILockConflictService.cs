using Suanpan.Services.Calendar.Conflicts.Models;
using Suanpan.Shared.Mvc;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Calendar.Conflicts;

/// <summary>
/// 锁定冲突的处理（日历设计 §3.9，FR-011）：编辑对比锁定值和采集值，选择保持锁定或解锁并采用采集值。
/// 处理时带读取时事件的 row_version，事件被他人或采集改过时拒绝（ERR-EVT-009）
/// </summary>
public interface ILockConflictService
{
    /// <summary>待处理的按最近检测时间倒序，已处理的按处理时间倒序</summary>
    Task<PagedResult<LockConflictRow>> QueryAsync(LockConflictQuery query, CancellationToken ct = default);

    /// <summary>保持锁定：冲突改为已处理，字段不变；之后在这把锁下采集到同一个值不再产生冲突</summary>
    Task<ManageResult> KeepAsync(long id, int rowVersion, CancellationToken ct = default);

    /// <summary>
    /// 解锁并采用采集值：移除锁定，写入采集值（公布值连同来源数据源、快照、检测时间），写"解锁"和该字段的变更历史；
    /// 改的是公布过的事件时发出 EventValueEditedEvent（BR-052）
    /// </summary>
    Task<ManageResult> AdoptAsync(long id, int rowVersion, CancellationToken ct = default);
}
