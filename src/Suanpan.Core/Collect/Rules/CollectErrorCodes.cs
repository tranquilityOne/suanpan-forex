namespace Suanpan.Core.Collect.Rules;

/// <summary>
/// 运行记录的错误码（采集设计 §9，写入 col_run.error_code）。因数据源或任务已停用而跳过、缺少解析器等实现时没有对应的错误码，只写原因
/// </summary>
public static class CollectErrorCodes
{
    /// <summary>请求失败，重试用完或不可重试</summary>
    public const string RequestFailed = "ERR-COL-002";

    /// <summary>同一任务上一次运行还没有结束，本次跳过（BR-010）</summary>
    public const string ConcurrentRun = "ERR-COL-004";

    /// <summary>快照写入失败：不解析、不重试，记为失败并告警（采集设计 §4.3）</summary>
    public const string SnapshotWriteFailed = "ERR-COL-007";

    /// <summary>排队超过 10 分钟仍未开始：下发失败（采集设计 §4.2）</summary>
    public const string DispatchFailed = "ERR-COL-008";

    /// <summary>执行中断：进程退出或重试消息丢失，运行记录停在"运行中"（采集设计 §4.3）</summary>
    public const string Interrupted = "ERR-COL-009";

    /// <summary>数据源当日的请求数达到每日上限（BR-003）：不发请求，记为失败、不重试（采集设计 §4.3）</summary>
    public const string DailyLimitExceeded = "ERR-COL-010";

    /// <summary>盯盘窗口结束仍没拿到值：官方延迟（日历的错误码，盯盘的运行记录用它表示结果，采集设计 §5.7）</summary>
    public const string OfficialDelay = "ERR-EVT-001";

    /// <summary>解析失败，不入库</summary>
    public const string ParseFailed = "ERR-PARSE-001";

    /// <summary>部分记录缺失或入库失败，记为部分成功</summary>
    public const string PartialData = "ERR-PARSE-002";

    /// <summary>快照无效：内容为空或被判定为拦截页，记为失败（采集设计 §5.5）</summary>
    public const string InvalidSnapshot = "ERR-PARSE-003";
}
