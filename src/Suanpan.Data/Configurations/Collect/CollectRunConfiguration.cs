using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;

namespace Suanpan.Data.Configurations.Collect;

/// <summary>运行记录：系统写入，不带审计字段（采集设计 §2.1）</summary>
public class CollectRunConfiguration : IEntityTypeConfiguration<CollectRun>
{
    public void Configure(EntityTypeBuilder<CollectRun> builder)
    {
        builder.ToTable("col_run");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TaskId).HasColumnName("task_id");
        builder.Property(x => x.SourceId).HasColumnName("source_id");
        builder.Property(x => x.Trigger).HasColumnName("trigger").HasConversion<short>();
        builder.Property(x => x.ScheduledFor).HasColumnName("scheduled_for");
        builder.Property(x => x.TriggeredBy).HasColumnName("triggered_by").HasMaxLength(64);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<short>();
        builder.Property(x => x.QueuedAt).HasColumnName("queued_at");
        builder.Property(x => x.StartedAt).HasColumnName("started_at");
        builder.Property(x => x.EndedAt).HasColumnName("ended_at");
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count");
        builder.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.ComplexCollection(x => x.Attempts, attempts => attempts.ToJson("attempts"));
        builder.Property(x => x.InsertedCount).HasColumnName("inserted_count");
        builder.Property(x => x.UpdatedCount).HasColumnName("updated_count");
        builder.Property(x => x.SkippedCount).HasColumnName("skipped_count");
        builder.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(32);
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        builder.Property(x => x.TraceId).HasColumnName("trace_id").HasMaxLength(64);

        // 同一任务同一 cron 时间点只创建一次运行：调度分发重复执行时不会重复下发（采集设计 §4.2）
        builder.HasIndex(x => new { x.TaskId, x.ScheduledFor }).IsUnique().HasFilter("scheduled_for IS NOT NULL");

        // 按任务、数据源、时间查看运行记录；删除任务前检查；清理过期记录
        builder.HasIndex(x => new { x.TaskId, x.QueuedAt });
        builder.HasIndex(x => new { x.SourceId, x.QueuedAt });
        builder.HasIndex(x => x.QueuedAt);

        // 找排队、运行中的记录：并发检查、超时回收
        builder.HasIndex(x => x.Status).HasFilter($"status IN ({(short)CollectRunStatus.Queued}, {(short)CollectRunStatus.Running})");
    }
}
