namespace Suanpan.Core.Collect.Rules;

/// <summary>运行记录的错误码（采集设计 §9，写入 col_run.error_code）；其余错误码随 Collector 的执行框架（FR-002.4）加入</summary>
public static class CollectErrorCodes
{
    /// <summary>排队超过 10 分钟仍未开始：下发失败（采集设计 §4.2）</summary>
    public const string DispatchFailed = "ERR-COL-008";
}
