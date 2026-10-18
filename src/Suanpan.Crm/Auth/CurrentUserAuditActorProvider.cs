using Suanpan.Data.Interceptors;
using Suanpan.Shared.Auth;

namespace Suanpan.Crm.Auth;

/// <summary>审计字段的操作人取当前登录名；没有登录用户时（如登录过程中更新失败次数）记为 crm</summary>
public sealed class CurrentUserAuditActorProvider(ICurrentUser currentUser) : IAuditActorProvider
{
    public string GetActor() => currentUser.UserName ?? "crm";
}
