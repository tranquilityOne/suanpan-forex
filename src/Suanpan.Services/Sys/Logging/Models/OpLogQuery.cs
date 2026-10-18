namespace Suanpan.Services.Sys.Logging.Models;

/// <summary>
/// 操作日志查询条件（sys 设计 §7.3）：操作人按登录名包含匹配，模块、动作精确匹配；
/// 时间为 UTC，Begin 含、End 不含。
/// </summary>
public sealed record OpLogQuery(
    string? Username = null,
    string? Module = null,
    string? Action = null,
    bool? IsSuccess = null,
    DateTime? Begin = null,
    DateTime? End = null,
    int Page = 1,
    int PageSize = 20);
