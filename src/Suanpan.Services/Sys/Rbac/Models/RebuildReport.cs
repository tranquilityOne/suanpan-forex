namespace Suanpan.Services.Sys.Rbac.Models;

/// <summary>权限树重建结果；停用的权限码和缺少 [PermMenu] 的控制器供页面展示，便于人工核查</summary>
public sealed record RebuildReport(
    int Added,
    int Updated,
    int Disabled,
    IReadOnlyList<string> DisabledCodes,
    IReadOnlyList<string> MissingMenuControllers);
