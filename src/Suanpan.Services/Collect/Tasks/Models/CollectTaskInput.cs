namespace Suanpan.Services.Collect.Tasks.Models;

/// <summary>
/// 新增或修改采集任务（采集设计 §3.2）。目标数据类型由解析器决定，不在输入中（BR-012）；
/// 启用状态不在这里改，见 SetEnabledAsync
/// </summary>
public sealed record CollectTaskInput
{
    public required string Name { get; init; }

    public required long SourceId { get; init; }

    public required string ParserKey { get; init; }

    /// <summary>5 段 cron，按 UTC 解释；为空表示只能手动触发</summary>
    public string? Cron { get; init; }

    /// <summary>解析器参数，JSON 对象</summary>
    public string? Params { get; init; }

    public string? Remark { get; init; }
}
