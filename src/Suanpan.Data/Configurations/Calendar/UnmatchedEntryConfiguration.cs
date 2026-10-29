using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Calendar.Entities;

namespace Suanpan.Data.Configurations.Calendar;

/// <summary>未匹配条目：系统写入，不带审计字段（日历设计 §2.1、§4.5）</summary>
public class UnmatchedEntryConfiguration : IEntityTypeConfiguration<UnmatchedEntry>
{
    public void Configure(EntityTypeBuilder<UnmatchedEntry> builder)
    {
        builder.ToTable("cal_unmatched_entry");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SourceId).HasColumnName("source_id");
        builder.Property(x => x.ReleaseKey).HasColumnName("release_key").HasMaxLength(128);
        builder.Property(x => x.ReleaseName).HasColumnName("release_name").HasMaxLength(256);
        builder.Property(x => x.ReferencePeriod).HasColumnName("reference_period").HasMaxLength(16);
        builder.Property(x => x.LocalDate).HasColumnName("local_date");
        builder.Property(x => x.LocalTime).HasColumnName("local_time");
        builder.Property(x => x.TimePrecision).HasColumnName("time_precision").HasConversion<short>();
        builder.Property(x => x.FirstSeenAt).HasColumnName("first_seen_at");
        builder.Property(x => x.LastSeenAt).HasColumnName("last_seen_at");
        builder.Property(x => x.LastRunId).HasColumnName("last_run_id");

        // 同一条目重复出现时更新；也用于按数据源查看、删除数据源前检查引用
        builder.HasIndex(x => new { x.SourceId, x.ReleaseKey, x.ReferencePeriod }).IsUnique();
    }
}
