namespace Suanpan.Shared.Auth;

/// <summary>
/// 当前登录的后台用户。没有登录用户时（未登录、后台任务、启动期）各属性为空；
/// 审计字段中的程序名由数据层的 IAuditActorProvider 负责，不在这里回落。
/// </summary>
public interface ICurrentUser
{
    /// <summary>用户 id；未登录时为空。</summary>
    long? UserId { get; }

    /// <summary>登录名；未登录时为空。</summary>
    string? UserName { get; }
}
