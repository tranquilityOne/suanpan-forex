namespace Suanpan.Services.Sys.Auth;

/// <summary>
/// 安全戳校验（sys 设计 §5.3）：Cookie 中间件在 OnValidatePrincipal 中逐请求调用，结果缓存 60 秒。
/// 修改密码、重置密码、停用账号、绑定或重置 TOTP 时刷新安全戳并调用 <see cref="Invalidate"/>，旧 Cookie 随即失效。
/// </summary>
public interface ISecurityStampValidator
{
    /// <summary>Cookie 中的安全戳是否与库中一致；账号不存在或已停用时返回 false</summary>
    Task<bool> ValidateAsync(long userId, string? stamp, CancellationToken ct = default);

    /// <summary>让该账号的缓存失效，新的安全戳立即生效</summary>
    void Invalidate(long userId);
}
