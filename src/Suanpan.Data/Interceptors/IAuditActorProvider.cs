namespace Suanpan.Data.Interceptors;

/// <summary>提供写入审计字段的操作人。</summary>
public interface IAuditActorProvider
{
    /// <summary>后台请求返回登录用户的登录名；没有登录用户时返回程序名，如 collector、job。</summary>
    string GetActor();
}
