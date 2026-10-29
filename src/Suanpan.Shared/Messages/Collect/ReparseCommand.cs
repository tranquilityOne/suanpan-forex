namespace Suanpan.Shared.Messages.Collect;

/// <summary>
/// 执行一条重新解析的运行记录（采集设计 §5.6、§7.3）。Crm 先创建"排队"的运行记录（解析器和快照 id 记在 reparse 列，不放进消息）再下发；
/// 接收方只处理"排队"的记录，其他状态是重复投递（消费端幂等）
/// </summary>
/// <param name="RunId">运行记录 id（col_run），触发方式为"重新解析"</param>
public sealed record ReparseCommand(long RunId) : IntegrationMessage;
