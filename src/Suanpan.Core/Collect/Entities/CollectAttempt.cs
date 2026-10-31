namespace Suanpan.Core.Collect.Entities;

/// <summary>运行记录中的一次请求（col_run.attempts，jsonb 数组），运行详情据此显示每次重试的时间和原因（FR-002 验收第 3 条）</summary>
public sealed class CollectAttempt
{
    /// <summary>第几次请求，从 1 开始</summary>
    public short No { get; set; }

    public DateTime StartedAt { get; set; }

    public int DurationMs { get; set; }

    /// <summary>HTTP 状态码；超时、网络错误时为空</summary>
    public int? HttpStatus { get; set; }

    /// <summary>失败原因；成功时为空</summary>
    public string? Error { get; set; }

    /// <summary>因数据源频率上限等待的毫秒数（ERR-COL-003），运行详情显示"已延后"</summary>
    public int DeferredMs { get; set; }

    /// <summary>盯盘：这次响应的内容与上一次相比有没有变化（只有变化时才解析，BR-015）；其他触发方式为空</summary>
    public bool? Changed { get; set; }
}
