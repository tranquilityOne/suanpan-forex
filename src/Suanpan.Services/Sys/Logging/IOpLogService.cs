using Suanpan.Core.Sys.Entities;
using Suanpan.Services.Sys.Logging.Models;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Sys.Logging;

/// <summary>
/// 操作日志（sys 设计 §7，FR-029）：只追加，没有修改和删除。
/// 只记录请求参数，不记录字段修改前后的值（DEC-018）。
/// </summary>
public interface IOpLogService
{
    /// <summary>写入一条操作日志；created_at 取当前 UTC 时间，超长的字段截断到列的长度上限</summary>
    Task AddAsync(SysOpLog log, CancellationToken ct = default);

    Task<PagedResult<OpLogRow>> QueryAsync(OpLogQuery query, CancellationToken ct = default);

    /// <summary>按筛选条件导出 CSV（UTF-8 带 BOM），按时间倒序，最多 <see cref="LogExport.MaxRows"/> 行；返回导出的行数</summary>
    Task<int> ExportAsync(OpLogQuery query, Stream output, TimeZoneInfo timeZone, CancellationToken ct = default);
}
