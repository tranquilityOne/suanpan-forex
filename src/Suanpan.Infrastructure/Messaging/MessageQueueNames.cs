namespace Suanpan.Infrastructure.Messaging;

/// <summary>
/// 交换机和队列的命名（docs/conventions/backend.md §8.2）。
/// 所有消息发到同一个 direct 交换机，路由键为消息类型名。每个接收程序有自己的队列，
/// 不同程序各收一份，同一程序的多个实例分摊处理。
/// </summary>
public static class MessageQueueNames
{
    public const string Exchange = "suanpan.messages";

    /// <summary>接收队列：suanpan.{程序}.{消息名}</summary>
    public static string Queue(string appName, string messageName) => $"suanpan.{appName}.{messageName}";

    /// <summary>重试次数用完后转入的队列，等待人工处理</summary>
    public static string Failed(string queue) => queue + ".failed";

    /// <summary>重试队列：消息在这里等待指定秒数后回到原接收队列。等待时长写进队列名，修改时长不会与旧队列冲突</summary>
    public static string Retry(string queue, int delaySeconds) => $"{queue}.retry.{delaySeconds}s";

    /// <summary>延时发送队列：消息在这里等待指定秒数后进入交换机，按原路由键分发</summary>
    public static string Delay(string messageName, long delaySeconds) => $"suanpan.delay.{messageName}.{delaySeconds}s";
}
