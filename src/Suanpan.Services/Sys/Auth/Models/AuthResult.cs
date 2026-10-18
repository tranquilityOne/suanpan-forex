using Suanpan.Core.Sys.Entities;
using Suanpan.Core.Sys.Enums;

namespace Suanpan.Services.Sys.Auth.Models;

/// <summary>认证某一步的结果：成功时给出账号和下一步；失败时给出面向用户的提示</summary>
public sealed record AuthResult(AuthUser? User, SignInStep Next, string? Error)
{
    public bool Success => Error is null;

    public static AuthResult Ok(SysUser user, SignInStep next) => new(AuthUser.From(user), next, null);

    public static AuthResult Fail(string error) => new(null, default, error);
}
