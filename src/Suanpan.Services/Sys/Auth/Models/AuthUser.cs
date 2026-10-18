using Suanpan.Core.Sys.Entities;

namespace Suanpan.Services.Sys.Auth.Models;

/// <summary>签发 Cookie 需要的账号信息（sys 设计 §5.2）</summary>
public sealed record AuthUser(long Id, string Username, string RealName, string SecurityStamp)
{
    public static AuthUser From(SysUser user) => new(user.Id, user.Username, user.RealName, user.SecurityStamp);
}
