using Suanpan.Crm.Mvc;
using Suanpan.Services.Sys.Logging.Models;

namespace Suanpan.Crm.Models.Sys;

/// <summary>
/// 操作日志的筛选条件（sys 设计 §7.3），列表和导出共用。日期按操作者时区选，包含两端。
/// 动作参数叫 ActionName 而不是 Action：路由值 action 优先于查询参数，同名会绑定成 "Index"。
/// </summary>
public sealed class OpLogFilter
{
    public string? Username { get; set; }

    public string? Module { get; set; }

    public string? ActionName { get; set; }

    public bool? IsSuccess { get; set; }

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public OpLogQuery ToQuery(TimeZoneInfo timeZone, int page = 1)
    {
        var (begin, end) = DateRangeFilter.ToUtc(From, To, timeZone);
        return new OpLogQuery(Username, Module, ActionName, IsSuccess, begin, end, page);
    }
}
