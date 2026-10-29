using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Calendar.Entities;

namespace Suanpan.Data.Configurations.Calendar;

/// <summary>各数据源的数值：系统写入，不带审计字段（日历设计 §2.1）</summary>
public class CalendarEventValueConfiguration : IEntityTypeConfiguration<CalendarEventValue>
{
    public void Configure(EntityTypeBuilder<CalendarEventValue> builder)
    {
        builder.ToTable("cal_event_value");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EventId).HasColumnName("event_id");
        builder.Property(x => x.SourceId).HasColumnName("source_id");
        builder.Property(x => x.Value).HasColumnName("value").HasPrecision(18, 6);
        builder.Property(x => x.ValueUpper).HasColumnName("value_upper").HasPrecision(18, 6);
        builder.Property(x => x.Raw).HasColumnName("raw").HasMaxLength(1000);
        builder.Property(x => x.SnapshotId).HasColumnName("snapshot_id");
        builder.Property(x => x.RunId).HasColumnName("run_id");
        builder.Property(x => x.DetectedAt).HasColumnName("detected_at");

        // 每个事件每个数据源一行
        builder.HasIndex(x => new { x.EventId, x.SourceId }).IsUnique();

        // 删除数据源前检查引用
        builder.HasIndex(x => x.SourceId);
    }
}
