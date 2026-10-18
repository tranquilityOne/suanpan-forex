namespace Suanpan.Crm.Models.Shared;

/// <summary>空状态部件 _EmptyState 的参数：提示文字，以及可选的操作入口（如"新增"）</summary>
public sealed record EmptyStateModel(string Message, string? ActionText = null, string? ActionUrl = null, string Icon = "database-off");
