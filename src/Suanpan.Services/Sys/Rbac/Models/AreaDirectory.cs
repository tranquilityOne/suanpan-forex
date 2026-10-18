namespace Suanpan.Services.Sys.Rbac.Models;

/// <summary>栏目（Area）目录节点的元数据，由 Crm 的 AreaCatalog 传入（sys 设计 §6.2）</summary>
public sealed record AreaDirectory(string Area, string Name, string? Icon, int Sort);
