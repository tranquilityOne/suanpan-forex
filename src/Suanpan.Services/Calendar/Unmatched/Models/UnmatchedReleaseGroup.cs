using Suanpan.Core.Calendar.Enums;

namespace Suanpan.Services.Calendar.Unmatched.Models;

/// <summary>
/// 一个数据源的一个发布标识下的未匹配条目（日历设计 §9.1）。发布名称取最近出现的条目；
/// 当地日期时间按数据源所属经济体的时区（<see cref="TimeZone"/>，数据源没有所属经济体时为空）
/// </summary>
public sealed record UnmatchedReleaseGroup(
    long SourceId,
    string SourceName,
    string? TimeZone,
    string ReleaseKey,
    string ReleaseName,
    DateTime LastSeenAt,
    IReadOnlyList<UnmatchedEntryRow> Entries);

/// <summary>未匹配的一期：参考期和官方日程中的当地日期时间</summary>
public sealed record UnmatchedEntryRow(string ReferencePeriod, DateOnly? LocalDate, TimeOnly? LocalTime, TimePrecision TimePrecision);

/// <summary>已忽略的发布，记录谁、什么时候忽略的</summary>
public sealed record IgnoredReleaseRow(long Id, long SourceId, string SourceName, string ReleaseKey, string ReleaseName, string CreatedBy, DateTime CreatedAt);
