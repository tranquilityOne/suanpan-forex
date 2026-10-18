using Suanpan.Core.Sys.Enums;

namespace Suanpan.Services.Sys.Logging.Models;

/// <summary>登录日志列表的一行（时间为 UTC）</summary>
public sealed record LoginLogRow(
    long Id,
    DateTime CreatedAt,
    string Username,
    long? UserId,
    LoginStage Stage,
    bool IsSuccess,
    string? FailReason,
    string? Ip,
    string? UserAgent);
