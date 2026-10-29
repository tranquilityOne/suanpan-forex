namespace Suanpan.Services.Calendar.Unmatched.Models;

/// <summary>未匹配条目的筛选条件（日历设计 §9.1）：按发布分组分页</summary>
public sealed record UnmatchedEntryQuery(long? SourceId = null, int Page = 1, int PageSize = 20);

/// <summary>已忽略的发布的筛选条件</summary>
public sealed record IgnoredReleaseQuery(long? SourceId = null, int Page = 1, int PageSize = 20);
