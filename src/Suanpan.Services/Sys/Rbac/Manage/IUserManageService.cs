using Suanpan.Core.Sys.Enums;
using Suanpan.Services.Sys.Rbac.Manage.Models;
using Suanpan.Shared.Mvc;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Sys.Rbac.Manage;

/// <summary>
/// 后台账号管理（sys 设计 §8.2、§9.1）。不提供删除：离职一律停用。
/// 角色变更、启停后，该账号的权限快照立即失效；停用、重置密码、重置二次验证会刷新安全戳，已登录的会话立即失效。
/// 解锁在 ENG-005.2 中加入。
/// </summary>
public interface IUserManageService
{
    Task<PagedResult<UserRow>> QueryAsync(UserQuery query, CancellationToken ct = default);

    Task<UserDetail?> GetAsync(long userId, CancellationToken ct = default);

    /// <summary>新建账号：状态为待激活，首次登录必须改密码并绑定 TOTP</summary>
    Task<ManageResult> CreateAsync(UserCreateInput input, CancellationToken ct = default);

    /// <summary>修改姓名、邮箱，整体覆盖角色；不能去掉最后一个管理员的管理员角色</summary>
    Task<ManageResult> UpdateAsync(UserUpdateInput input, CancellationToken ct = default);

    /// <summary>
    /// 启用或停用。停用：不能停用自己，不能停用最后一个管理员；启用：只能从"停用"恢复。
    /// 不能手工设为"待激活"。
    /// </summary>
    Task<ManageResult> SetStatusAsync(long userId, UserStatus status, CancellationToken ct = default);

    /// <summary>重置密码：之后首次登录必须改密码</summary>
    Task<ManageResult> ResetPasswordAsync(long userId, string newPassword, CancellationToken ct = default);

    /// <summary>重置二次验证：清空 TOTP 密钥，下次登录时必须重新绑定</summary>
    Task<ManageResult> ResetTotpAsync(long userId, CancellationToken ct = default);
}
