using Suanpan.Core.Calendar.Enums;
using Suanpan.Services.Calendar.Events.Models;
using Suanpan.Shared.Mvc;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Calendar.Events;

/// <summary>
/// 日历事件的查看和人工处理（日历设计 §8.2）：事件列表和详情（FR-009）；待确认事件的保留、取消（BR-040，§4.4）；人工修改随 FR-009.2 扩充。
/// 修改都带读取时的 row_version，事件已被他人或采集改过时拒绝（ERR-EVT-009）；变更历史记为人工
/// </summary>
public interface ICalendarEventService
{
    /// <summary>取消原因的长度上限，与变更历史的原因列一致</summary>
    const int MaxReasonLength = 500;

    /// <summary>事件列表（BR-048）：按排期时间升序，没有排期时间的在最后</summary>
    Task<PagedResult<EventRow>> QueryAsync(EventQuery query, CancellationToken ct = default);

    /// <summary>事件详情（FR-009 主流程第 2 步）；不存在时为空</summary>
    Task<EventDetail?> GetDetailAsync(long id, CancellationToken ct = default);

    /// <summary>状态为"待确认"的事件，按排期时间排序</summary>
    Task<PagedResult<UnconfirmedEventRow>> QueryUnconfirmedAsync(UnconfirmedEventQuery query, CancellationToken ct = default);

    /// <summary>一个待确认事件；不存在或已不是待确认时为空。取消页显示事件信息用</summary>
    Task<UnconfirmedEventRow?> GetUnconfirmedAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// 保留：改回"待公布"，并与数据源脱钩（维护排期的数据源和发布标识置空），以后不再被这个数据源判为消失；
    /// 重新出现在日程中时由数据源接管（§4.3）。只能保留待确认的事件
    /// </summary>
    Task<ManageResult> KeepAsync(long id, int rowVersion, CancellationToken ct = default);

    /// <summary>取消：原因必填（ERR-EVT-008，失败时 Field 为 Reason），写进变更历史；状态须允许改为"取消"（ERR-EVT-010）</summary>
    Task<ManageResult> CancelAsync(long id, string? reason, int rowVersion, CancellationToken ct = default);

    /// <summary>
    /// 人工修改公布值、前值、前值修正（日历设计 §3.7，BR-050）：原因必填；只改与当前值不同的字段，改了的字段自动锁定；
    /// 录入公布值后未公布、待复核的事件改为"已公布"并把公布值填为下一期的前值。格式不对时 Field 为对应的字段名（ERR-FC-001 同样的规则）
    /// </summary>
    Task<ManageResult> EditValuesAsync(long id, EventValuesInput input, string? reason, int rowVersion, CancellationToken ct = default);

    /// <summary>解锁一个字段（BR-063）：下一次采集照常更新；这个字段待处理的锁定冲突一并关闭（处理方式"已解锁"）</summary>
    Task<ManageResult> UnlockAsync(long id, EventField field, int rowVersion, CancellationToken ct = default);

    /// <summary>待复核的事件，按排期时间排序，带进入待复核的原因和各数据源的候选值</summary>
    Task<PagedResult<ReviewRow>> QueryReviewAsync(int page = 1, int pageSize = 20, CancellationToken ct = default);

    /// <summary>待复核：确认采集值（采用新值，BR-051）——把所选数据源的候选值写入公布值并改为"已公布"，不锁定</summary>
    Task<ManageResult> ConfirmReviewAsync(long id, long sourceId, int rowVersion, CancellationToken ct = default);

    /// <summary>待复核：驳回（BR-051）——改为"延迟"继续补采；原因必填，只适用于从未公布过的事件</summary>
    Task<ManageResult> RejectReviewAsync(long id, string? reason, int rowVersion, CancellationToken ct = default);
}
