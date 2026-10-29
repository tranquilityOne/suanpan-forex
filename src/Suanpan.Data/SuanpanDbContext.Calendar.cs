using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Calendar.Entities;

namespace Suanpan.Data;

/// <summary>财经日历模块：事件、各数据源的数值、变更历史、锁定冲突、未匹配条目、已忽略的发布（日历设计 §2）</summary>
public partial class SuanpanDbContext
{
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();

    public DbSet<CalendarEventValue> CalendarEventValues => Set<CalendarEventValue>();

    public DbSet<CalendarEventChange> CalendarEventChanges => Set<CalendarEventChange>();

    public DbSet<LockConflict> LockConflicts => Set<LockConflict>();

    public DbSet<UnmatchedEntry> UnmatchedEntries => Set<UnmatchedEntry>();

    public DbSet<IgnoredRelease> IgnoredReleases => Set<IgnoredRelease>();
}
