using Suanpan.Services.Sys.Rbac;
using Suanpan.Shared.Auth;

namespace Suanpan.Crm.Auth;

/// <summary>
/// 当前用户在本次请求中的权限（sys 设计 §6.6），只用于决定界面上显示哪些按钮；权限以后端校验为准（BR-145）。
/// 按请求注册，一次请求只取一次权限快照。标签助手 asp-permission 用它；一个元素要看多个权限码时，视图里注入它直接判断。
/// </summary>
public sealed class CurrentPermissions(ICurrentUser currentUser, IPermissionService permissions)
{
    private PermissionSnapshot? _snapshot;

    /// <summary>有该权限码时返回 true；管理员总是 true，未登录总是 false</summary>
    public async Task<bool> HasAsync(string permCode, CancellationToken ct = default)
    {
        if (currentUser.UserId is not { } userId)
        {
            return false;
        }

        _snapshot ??= await permissions.GetSnapshotAsync(userId, ct);
        return _snapshot.Has(permCode);
    }
}
