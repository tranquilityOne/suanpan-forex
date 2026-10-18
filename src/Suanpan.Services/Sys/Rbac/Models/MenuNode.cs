namespace Suanpan.Services.Sys.Rbac.Models;

/// <summary>侧边栏菜单节点：目录（Area）或菜单（Controller）</summary>
public sealed record MenuNode(long Id, string Name, string? Url, string? Icon, IReadOnlyList<MenuNode> Children);
