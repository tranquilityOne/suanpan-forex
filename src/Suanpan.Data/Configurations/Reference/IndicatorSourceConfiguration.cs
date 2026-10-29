using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Reference.Entities;
using Suanpan.Core.Reference.Enums;
using Suanpan.Core.Reference.Rules;
using Suanpan.Data.Configurations.Common;

namespace Suanpan.Data.Configurations.Reference;

public class IndicatorSourceConfiguration : IEntityTypeConfiguration<IndicatorSource>
{
    public void Configure(EntityTypeBuilder<IndicatorSource> builder)
    {
        builder.ToTable("ref_indicator_source");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IndicatorCode).HasColumnName("indicator_code").HasMaxLength(ReferenceCodeRule.MaxIndicatorCodeLength).IsRequired();
        builder.Property(x => x.SourceId).HasColumnName("source_id");
        builder.Property(x => x.Purpose).HasColumnName("purpose").HasConversion<short>();
        builder.Property(x => x.ExternalId).HasColumnName("external_id").HasMaxLength(128).IsRequired();
        builder.Property(x => x.ParserKey).HasColumnName("parser_key").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Priority).HasColumnName("priority");
        builder.HasAuditColumns();

        // 一个指标对同一数据源每种用途只映射一次（PRD §8.1.2）；同一指标公布值映射的优先级不重复，主备顺序明确（基础数据设计 §2.2）
        builder.HasIndex(x => new { x.IndicatorCode, x.SourceId, x.Purpose }).IsUnique();
        builder.HasIndex(x => new { x.IndicatorCode, x.Priority }).IsUnique().HasFilter($"purpose = {(short)IndicatorSourcePurpose.Release}");

        // 入库时按数据源、用途、外部标识找指标（日历设计 §4.2、§5.2）；也用于删除数据源前检查引用
        builder.HasIndex(x => new { x.SourceId, x.Purpose, x.ExternalId });
    }
}
