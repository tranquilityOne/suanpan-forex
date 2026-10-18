namespace Suanpan.Services.Sys.Rbac.Models;

/// <summary>
/// 权限树重建结果；停用的、重新启用的权限码和缺少 [PermMenu] 的控制器供页面展示，便于人工核查。
/// 重新启用包括代码中删掉又加回的，以及在页面上手工停用过的（重建以代码为准，sys 设计 §6.3）。
/// </summary>
public sealed record RebuildReport(
    int Added,
    int Updated,
    int Disabled,
    IReadOnlyList<string> DisabledCodes,
    IReadOnlyList<string> MissingMenuControllers,
    IReadOnlyList<string> ReEnabledCodes);
