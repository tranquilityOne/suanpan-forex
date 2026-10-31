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
}
