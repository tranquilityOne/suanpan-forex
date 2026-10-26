namespace Suanpan.Shared.Messages.Collect;

/// <summary>
/// 执行一条采集运行记录（采集设计 §4、§7.3）。定时到点和手动触发都由发送方（Job、Crm）先创建"排队"的运行记录再下发；
/// Collector 重试时延时发给自己，Attempt 加一。接收方按运行记录的状态和 Attempt 判断重复投递（消费端幂等）
/// </summary>
/// <param name="RunId">运行记录 id（col_run）</param>
/// <param name="Attempt">第几次请求，从 1 开始</param>
public sealed record CollectRunCommand(long RunId, int Attempt) : IntegrationMessage;
