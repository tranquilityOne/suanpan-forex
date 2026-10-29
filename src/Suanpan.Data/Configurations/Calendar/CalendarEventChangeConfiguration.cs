using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Calendar.Entities;

namespace Suanpan.Data.Configurations.Calendar;

/// <summary>变更历史：只追加，不带审计字段（日历设计 §2.1）</summary>
public class CalendarEventChangeConfiguration : IEntityTypeConfiguration<CalendarEventChange>
{
    public void Configure(EntityTypeBuilder<CalendarEventChange> builder)
    {
        builder.ToTable("cal_event_change");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EventId).HasColumnName("event_id");
        builder.Property(x => x.Kind).HasColumnName("kind").HasConversion<short>();
        builder.Property(x => x.OldValue).HasColumnName("old_value").HasColumnType("jsonb");
        builder.Property(x => x.NewValue).HasColumnName("new_value").HasColumnType("jsonb");
        builder.Property(x => x.ActorKind).HasColumnName("actor_kind").HasConversion<short>();
        builder.Property(x => x.SourceId).HasColumnName("source_id");
        builder.Property(x => x.RunId).HasColumnName("run_id");
        builder.Property(x => x.ChangedBy).HasColumnName("changed_by").HasMaxLength(64);
        builder.Property(x => x.ChangedAt).HasColumnName("changed_at");
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);

        // 事件详情的变更历史
        builder.HasIndex(x => x.EventId);
    }
}
