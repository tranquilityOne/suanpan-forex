using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Calendar.Entities;
using Suanpan.Data.Configurations.Common;

namespace Suanpan.Data.Configurations.Calendar;

/// <summary>已忽略的发布：带创建两件套，记录谁、什么时候忽略的（日历设计 §2.1、§4.5）</summary>
public class IgnoredReleaseConfiguration : IEntityTypeConfiguration<IgnoredRelease>
{
    public void Configure(EntityTypeBuilder<IgnoredRelease> builder)
    {
        builder.ToTable("cal_ignored_release");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SourceId).HasColumnName("source_id");
        builder.Property(x => x.ReleaseKey).HasColumnName("release_key").HasMaxLength(128);
        builder.Property(x => x.ReleaseName).HasColumnName("release_name").HasMaxLength(256);
        builder.HasCreationAuditColumns();

        // 同一数据源的同一发布标识只忽略一次；也用于删除数据源前检查引用
        builder.HasIndex(x => new { x.SourceId, x.ReleaseKey }).IsUnique();
    }
}
