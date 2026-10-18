using Suanpan.Crm.Mvc;
using Suanpan.Services.Sys.Logging.Models;

namespace Suanpan.Crm.Models.Sys;

/// <summary>登录日志的筛选条件（sys 设计 §7.3），列表和导出共用。日期按操作者时区选，包含两端。</summary>
public sealed class LoginLogFilter
{
    public string? Username { get; set; }

    public bool? IsSuccess { get; set; }

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public LoginLogQuery ToQuery(TimeZoneInfo timeZone, int page = 1)
    {
        var (begin, end) = DateRangeFilter.ToUtc(From, To, timeZone);
        return new LoginLogQuery(Username, IsSuccess, begin, end, page);
    }
}
