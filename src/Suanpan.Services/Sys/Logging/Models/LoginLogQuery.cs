namespace Suanpan.Services.Sys.Logging.Models;

/// <summary>登录日志查询条件（sys 设计 §7.3）：登录名包含匹配；时间为 UTC，Begin 含、End 不含</summary>
public sealed record LoginLogQuery(
    string? Username = null,
    bool? IsSuccess = null,
    DateTime? Begin = null,
    DateTime? End = null,
    int Page = 1,
    int PageSize = 20);
