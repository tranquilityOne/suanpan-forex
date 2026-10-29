using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Collect.Entities;

namespace Suanpan.Data.Configurations.Collect;

/// <summary>快照：系统写入，不带审计字段（采集设计 §2.1）</summary>
public class SnapshotConfiguration : IEntityTypeConfiguration<Snapshot>
{
    public void Configure(EntityTypeBuilder<Snapshot> builder)
    {
        builder.ToTable("col_snapshot");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RunId).HasColumnName("run_id");
        builder.Property(x => x.Attempt).HasColumnName("attempt");
        builder.Property(x => x.Seq).HasColumnName("seq");
        builder.Property(x => x.SourceId).HasColumnName("source_id");
        builder.Property(x => x.TaskId).HasColumnName("task_id");
        builder.Property(x => x.Url).HasColumnName("url").HasMaxLength(2048);
        builder.Property(x => x.RequestKey).HasColumnName("request_key").HasMaxLength(512);
        builder.Property(x => x.FetchedAt).HasColumnName("fetched_at");
        builder.Property(x => x.HttpStatus).HasColumnName("http_status");
        builder.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(128);
        builder.Property(x => x.ContentHash).HasColumnName("content_hash").HasColumnType("char(64)");
        builder.Property(x => x.Size).HasColumnName("size");
        builder.Property(x => x.Unchanged).HasColumnName("unchanged");
        builder.Property(x => x.IsValid).HasColumnName("is_valid");
        builder.Property(x => x.InvalidReason).HasColumnName("invalid_reason").HasMaxLength(500);
        builder.Property(x => x.Referenced).HasColumnName("referenced");

        // 运行详情列出关联快照
        builder.HasIndex(x => x.RunId);

        // 找同一数据源、同一请求标识的上一个快照，判断"未变化"（BR-021）
        builder.HasIndex(x => new { x.SourceId, x.RequestKey, x.Id });

        // 按任务、数据源、时间筛选；清理过期快照（采集设计 §4.6）
        builder.HasIndex(x => new { x.TaskId, x.FetchedAt });
        builder.HasIndex(x => new { x.SourceId, x.FetchedAt });
        builder.HasIndex(x => x.FetchedAt);

        // 清理不再被快照引用的内容
        builder.HasIndex(x => x.ContentHash);
    }
}
