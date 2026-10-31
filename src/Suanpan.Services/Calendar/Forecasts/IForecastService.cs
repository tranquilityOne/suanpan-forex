using Suanpan.Services.Calendar.Events.Models;
using Suanpan.Services.Calendar.Forecasts.Models;
using Suanpan.Shared.Mvc;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Calendar.Forecasts;

/// <summary>
/// 预期值的人工录入（日历设计 §3.8，FR-010）：公布前由管理员、编辑录入，公布后只有管理员能更正（BR-057）。
/// 保存时锁定预期值（BR-061）、写变更历史；带读取时的 row_version，被他人改过时拒绝（ERR-FC-003）
/// </summary>
public interface IForecastService
{
    /// <summary>
    /// 待录入清单（BR-055）：未公布（待公布、盯盘中、延迟、待确认）、预期值为空、排期在现在到 7 天后之间的事件，
    /// 默认只列高重要性的；按排期时间升序
    /// </summary>
    Task<PagedResult<EventRow>> QueryPendingAsync(ForecastQuery query, CancellationToken ct = default);

    /// <summary>清单中的一行（保存后只刷新这一行）；事件不存在时为空</summary>
    Task<EventRow?> GetRowAsync(long id, CancellationToken ct = default);

    /// <summary>来源说明的常用选项（系统配置 calendar:forecast_note_options，逗号分隔）</summary>
    Task<IReadOnlyList<string>> GetNoteOptionsAsync(CancellationToken ct = default);

    /// <summary>
    /// 公布前录入或修改（BR-055 ~ BR-058）：格式按指标（ERR-FC-001，Field 为 Forecast）；已公布的拒绝（ERR-FC-002）；
    /// 预期值和来源说明都没变时不改
    /// </summary>
    Task<ManageResult> SaveAsync(long id, ForecastInput input, int rowVersion, CancellationToken ct = default);

    /// <summary>公布后的更正（BR-057）：只给管理员的 action 调用，原因必填，写进变更历史和锁定信息</summary>
    Task<ManageResult> CorrectAsync(long id, ForecastInput input, string? reason, int rowVersion, CancellationToken ct = default);
}
