namespace Suanpan.Services.Sys.Rbac;

/// <summary>
/// 权限快照（sys 设计 §6.1、§6.5）：本机内存缓存 5 分钟。
/// 失效时机：账号的角色变更、启停（按账号）；角色授权变更、角色启停（持有该角色的全部账号）；
/// 权限点修改、权限树重建（全部）。
/// </summary>
public interface IPermissionService
{
    /// <summary>取账号的权限快照；账号不是"启用"状态时返回空快照</summary>
    Task<PermissionSnapshot> GetSnapshotAsync(long userId, CancellationToken ct = default);

    /// <summary>让一个账号的快照失效</summary>
    void Invalidate(long userId);

    /// <summary>让持有该角色的全部账号的快照失效</summary>
    Task InvalidateRoleHoldersAsync(long roleId, CancellationToken ct = default);

    /// <summary>让全部快照失效（实现为版本号递增，旧条目靠有效期自然淘汰）</summary>
    void InvalidateAll();
}
