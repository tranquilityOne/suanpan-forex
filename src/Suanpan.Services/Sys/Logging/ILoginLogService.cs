using Suanpan.Core.Sys.Entities;
using Suanpan.Services.Sys.Logging.Models;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Sys.Logging;

/// <summary>
/// 登录日志（sys 设计 §7.2）：认证服务在密码、TOTP、激活、退出每一步写入，成功和失败都记；只追加。
/// </summary>
public interface ILoginLogService
{
    /// <summary>写入一条登录日志；created_at 取当前 UTC 时间，超长的字段（如用户输入的登录名）截断到列的长度上限</summary>
    Task AddAsync(SysLoginLog log, CancellationToken ct = default);

    Task<PagedResult<LoginLogRow>> QueryAsync(LoginLogQuery query, CancellationToken ct = default);

    /// <summary>按筛选条件导出 CSV（UTF-8 带 BOM），按时间倒序，最多 <see cref="LogExport.MaxRows"/> 行；返回导出的行数</summary>
    Task<int> ExportAsync(LoginLogQuery query, Stream output, TimeZoneInfo timeZone, CancellationToken ct = default);
}
