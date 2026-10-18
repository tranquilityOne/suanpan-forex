namespace Suanpan.Infrastructure.Messaging;

/// <summary>RabbitMQ 配置（配置节 RabbitMq，docs/conventions/backend.md §12）</summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    /// <summary>AMQP URI（含账号密码），虚拟主机为 suanpan，只从 user-secrets 或环境变量读取</summary>
    public string Uri { get; set; } = string.Empty;

    /// <summary>处理失败后延迟重试的次数，用完后转入 .failed 队列</summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>每次重试前等待的秒数</summary>
    public int RetryDelaySeconds { get; set; } = 10;

    /// <summary>当前程序名（crm、job、collector、portal），用于队列名；由注册方法设置，不从配置读取</summary>
    public string AppName { get; set; } = string.Empty;
}
