using Suanpan.Shared.Mvc;

namespace Suanpan.Services.Sys.Rbac;

/// <summary>预置角色的"恢复默认授权"（sys 设计 §4.5）：按 <see cref="PresetRoleGrants"/> 重新授权，覆盖当前授权</summary>
public interface IPresetRoleGrantService
{
    Task<ManageResult> ApplyDefaultsAsync(string roleCode, CancellationToken ct = default);
}
