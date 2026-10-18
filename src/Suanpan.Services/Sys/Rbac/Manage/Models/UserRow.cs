using Suanpan.Core.Sys.Enums;

namespace Suanpan.Services.Sys.Rbac.Manage.Models;

/// <summary>账号列表的一行；LockedUntil 晚于当前时间即为锁定</summary>
public sealed record UserRow(
    long Id,
    string Username,
    string RealName,
    string? Email,
    UserStatus Status,
    DateTime? LockedUntil,
    IReadOnlyList<string> RoleNames,
    DateTime? LastLoginAt,
    DateTime CreatedAt);
