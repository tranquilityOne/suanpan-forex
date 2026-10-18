namespace Suanpan.Services.Sys.Logging.Models;

/// <summary>操作日志列表的一行（时间为 UTC）</summary>
public sealed record OpLogRow(
    long Id,
    DateTime CreatedAt,
    long UserId,
    string Username,
    string Roles,
    string Module,
    string Action,
    string? PermCode,
    string HttpMethod,
    string RequestUrl,
    string? RequestParams,
    string? Reason,
    bool IsSuccess,
    string? ErrorMsg,
    int DurationMs,
    string? Ip,
    string? UserAgent,
    string? TraceId);
