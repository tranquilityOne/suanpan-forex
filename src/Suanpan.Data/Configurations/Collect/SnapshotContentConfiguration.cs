using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Collect.Entities;

namespace Suanpan.Data.Configurations.Collect;

/// <summary>
/// 快照内容：按哈希去重（采集设计 §2.1、DEC-023）。内容已经 gzip 压缩，迁移中把 body 列的存储方式设为 EXTERNAL，
/// 不让 PostgreSQL 再压缩一次
/// </summary>
public class SnapshotContentConfiguration : IEntityTypeConfiguration<SnapshotContent>
{
    public void Configure(EntityTypeBuilder<SnapshotContent> builder)
    {
        builder.ToTable("col_snapshot_content");

        builder.HasKey(x => x.ContentHash);
        builder.Property(x => x.ContentHash).HasColumnName("content_hash").HasColumnType("char(64)");
        builder.Property(x => x.Body).HasColumnName("body");
        builder.Property(x => x.LastUsedAt).HasColumnName("last_used_at");
    }
}
