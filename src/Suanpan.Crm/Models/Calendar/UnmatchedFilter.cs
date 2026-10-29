using Suanpan.Services.Calendar.Unmatched.Models;

namespace Suanpan.Crm.Models.Calendar;

/// <summary>未匹配条目的筛选条件（日历设计 §9.1）：View 为 ignored 时显示已忽略的发布</summary>
public sealed class UnmatchedFilter
{
    public const string IgnoredView = "ignored";

    public long? SourceId { get; set; }

    public string? View { get; set; }

    public bool ShowIgnored => string.Equals(View, IgnoredView, StringComparison.OrdinalIgnoreCase);

    public UnmatchedEntryQuery ToQuery(int page = 1) => new(SourceId, page);

    public IgnoredReleaseQuery ToIgnoredQuery(int page = 1) => new(SourceId, page);
}
