namespace Suanpan.Core.Collect.Enums;

/// <summary>运行记录的状态（采集设计 §3.3）；流转规则见 CollectRunStatusRule</summary>
public enum CollectRunStatus : short
{
    /// <summary>排队：已创建，等待 Collector 开始执行</summary>
    Queued = 1,

    /// <summary>运行中，包括等待重试</summary>
    Running = 2,

    /// <summary>成功</summary>
    Succeeded = 3,

    /// <summary>部分成功：部分记录入库失败或缺失（ERR-PARSE-002）</summary>
    PartiallySucceeded = 4,

    /// <summary>失败</summary>
    Failed = 5,

    /// <summary>跳过：同一任务已在运行（BR-010），或任务、数据源已停用</summary>
    Skipped = 6,
}
