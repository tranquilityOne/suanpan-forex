using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Core.Calendar.Enums;
using Suanpan.Core.Reference.Rules;
using Suanpan.Data.Configurations.Common;

namespace Suanpan.Data.Configurations.Calendar;

/// <summary>日历事件（日历设计 §2.1、§2.2）</summary>
public class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    public void Configure(EntityTypeBuilder<CalendarEvent> builder)
    {
        builder.ToTable("cal_event");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IndicatorCode).HasColumnName("indicator_code").HasMaxLength(ReferenceCodeRule.MaxIndicatorCodeLength);
        builder.Property(x => x.ReferencePeriod).HasColumnName("reference_period").HasMaxLength(16);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<short>();
        builder.Property(x => x.Origin).HasColumnName("origin").HasConversion<short>();
        builder.Property(x => x.ReleaseBatchId).HasColumnName("release_batch_id").HasMaxLength(160);
        builder.Property(x => x.ScheduleSourceId).HasColumnName("schedule_source_id");
        builder.Property(x => x.ScheduleReleaseKey).HasColumnName("schedule_release_key").HasMaxLength(128);

        // 机构当地时间只保留原始值，不带时区（后端约定 §6.3）
        builder.Property(x => x.ScheduledLocal).HasColumnName("scheduled_local").HasColumnType("timestamp without time zone");
        builder.Property(x => x.TimeZone).HasColumnName("time_zone").HasMaxLength(64);
        builder.Property(x => x.ScheduledAt).HasColumnName("scheduled_at");
        builder.Property(x => x.TimePrecision).HasColumnName("time_precision").HasConversion<short>();
        builder.Property(x => x.LocalTimeResolution).HasColumnName("local_time_resolution").HasConversion<short>();

        builder.Property(x => x.Actual).HasColumnName("actual").HasPrecision(18, 6);
        builder.Property(x => x.ActualUpper).HasColumnName("actual_upper").HasPrecision(18, 6);
        builder.Property(x => x.ActualRaw).HasColumnName("actual_raw").HasMaxLength(1000);
        builder.Property(x => x.ActualSourceId).HasColumnName("actual_source_id");
        builder.Property(x => x.DetectedAt).HasColumnName("detected_at");
        builder.Property(x => x.SourceSnapshotId).HasColumnName("source_snapshot_id");

        builder.Property(x => x.Forecast).HasColumnName("forecast").HasPrecision(18, 6);
        builder.Property(x => x.ForecastUpper).HasColumnName("forecast_upper").HasPrecision(18, 6);
        builder.Property(x => x.ForecastType).HasColumnName("forecast_type").HasConversion<short>();
        builder.Property(x => x.ForecastNote).HasColumnName("forecast_note").HasMaxLength(100);

        builder.Property(x => x.Previous).HasColumnName("previous").HasPrecision(18, 6);
        builder.Property(x => x.PreviousUpper).HasColumnName("previous_upper").HasPrecision(18, 6);
        builder.Property(x => x.PreviousRevised).HasColumnName("previous_revised").HasPrecision(18, 6);
        builder.Property(x => x.RevisedValue).HasColumnName("revised_value").HasPrecision(18, 6);

        builder.ComplexCollection(x => x.LockedFields, locks => locks.ToJson("locked_fields"));
        builder.Property(x => x.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
        builder.HasAuditColumns();

        // 事件唯一键（BR-036），入库时按它 INSERT ... ON CONFLICT
        builder.HasIndex(x => new { x.IndicatorCode, x.ReferencePeriod }).IsUnique();

        // 按日期范围筛选（BR-048）；盯盘扫描未来 10 分钟的事件（FR-003）
        builder.HasIndex(x => x.ScheduledAt);

        // 判断从日程中消失（BR-040）；删除数据源前检查引用
        builder.HasIndex(x => new { x.ScheduleSourceId, x.ScheduleReleaseKey });
        builder.HasIndex(x => x.ActualSourceId);

        // 快讯按发布批次合并（BR-076）
        builder.HasIndex(x => x.ReleaseBatchId);

        // 待处理清单：待复核、延迟、待确认
        builder.HasIndex(x => x.Status).HasFilter(
            $"status IN ({(short)CalendarEventStatus.PendingReview}, {(short)CalendarEventStatus.Delayed}, {(short)CalendarEventStatus.Unconfirmed})");
    }
}
