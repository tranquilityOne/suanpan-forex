namespace Suanpan.Data.Interceptors;

/// <summary>操作人固定为程序名，用于 Collector、Job 等没有登录用户的程序。</summary>
public sealed class FixedAuditActorProvider(string actor) : IAuditActorProvider
{
    public string GetActor() => actor;
}
