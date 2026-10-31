using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;

namespace Suanpan.Data.Configurations.Calendar;

/// <summary>锁定冲突：系统写入，编辑处理时更新处理人和结果，不带审计字段（日历设计 §2.1，BR-062）</summary>
public class LockConflictConfiguration : IEntityTypeConfiguration<LockConflict>
{
    public void Configure(EntityTypeBuilder<LockConflict> builder)
    {
        builder.ToTable("cal_lock_conflict");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EventId).HasColumnName("event_id");
        builder.Property(x => x.Field).HasColumnName("field").HasConversion<short>();
        builder.Property(x => x.LockedValue).HasColumnName("locked_value").HasColumnType("jsonb");
        builder.Property(x => x.LockedUpper).HasColumnName("locked_upper").HasPrecision(18, 6);
        builder.Property(x => x.CollectedValue).HasColumnName("collected_value").HasColumnType("jsonb");
        builder.Property(x => x.CollectedUpper).HasColumnName("collected_upper").HasPrecision(18, 6);
        builder.Property(x => x.SourceId).HasColumnName("source_id");
        builder.Property(x => x.SnapshotId).HasColumnName("snapshot_id");
        builder.Property(x => x.RunId).HasColumnName("run_id");
        builder.Property(x => x.FirstDetectedAt).HasColumnName("first_detected_at");
        builder.Property(x => x.LastDetectedAt).HasColumnName("last_detected_at");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<short>();
        builder.Property(x => x.ResolvedBy).HasColumnName("resolved_by").HasMaxLength(64);
        builder.Property(x => x.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(x => x.Resolution).HasColumnName("resolution").HasConversion<short>();

        // 同一事件同一字段只有一条待处理的冲突，再次冲突时更新这一条
        builder.HasIndex(x => new { x.EventId, x.Field }).IsUnique().HasFilter($"status = {(short)LockConflictStatus.Pending}");

        // 冲突列表；删除数据源前检查引用
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.SourceId);
    }
}
