namespace Suanpan.Services.Collect.Tasks.Models;

/// <summary>cron 的预览（采集设计 §3.2）：写法错误时 Error 为提示；cron 为空（只能手动触发）时两者都为空</summary>
/// <param name="Next">接下来的执行时间（UTC），页面按操作者时区显示</param>
public sealed record CronPreview(string? Error, IReadOnlyList<DateTime> Next);
