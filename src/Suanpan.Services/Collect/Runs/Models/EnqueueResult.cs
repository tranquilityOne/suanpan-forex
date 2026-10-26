namespace Suanpan.Services.Collect.Runs.Models;

/// <summary>
/// 创建运行记录并下发 CollectRunCommand 的结果（采集设计 §4.4）。失败时 Error 为面向用户的提示，没有创建运行记录。
/// 运行记录创建后下发失败时 Success 仍为 true、Published 为 false：记录停在"排队"，由调度分发重新发布（采集设计 §4.2）
/// </summary>
public sealed record EnqueueResult(bool Success, string? Error, long? RunId, bool Published)
{
    public static EnqueueResult Fail(string error) => new(false, error, null, false);
}
