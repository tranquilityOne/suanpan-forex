using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Collect.Entities;
using Suanpan.Data.Configurations.Common;

namespace Suanpan.Data.Configurations.Collect;

public class CollectTaskConfiguration : IEntityTypeConfiguration<CollectTask>
{
    public void Configure(EntityTypeBuilder<CollectTask> builder)
    {
        builder.ToTable("col_task");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(64).IsRequired();
        builder.Property(x => x.SourceId).HasColumnName("source_id");
        builder.Property(x => x.ParserKey).HasColumnName("parser_key").HasMaxLength(64).IsRequired();
        builder.Property(x => x.TargetType).HasColumnName("target_type").HasConversion<short>();
        builder.Property(x => x.Cron).HasColumnName("cron").HasMaxLength(64);
        builder.Property(x => x.Params).HasColumnName("params").HasColumnType("jsonb");
        builder.Property(x => x.IsEnabled).HasColumnName("is_enabled");
        builder.Property(x => x.LastDispatchedAt).HasColumnName("last_dispatched_at");
        builder.Property(x => x.Remark).HasColumnName("remark").HasMaxLength(500);
        builder.HasAuditColumns();

        builder.HasIndex(x => x.Name).IsUnique();

        // 删除数据源前检查引用；数据源停用时找到它的任务
        builder.HasIndex(x => x.SourceId);
    }
}
