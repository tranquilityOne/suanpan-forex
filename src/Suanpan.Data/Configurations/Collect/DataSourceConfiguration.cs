using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Collect.Entities;
using Suanpan.Data.Configurations.Common;

namespace Suanpan.Data.Configurations.Collect;

public class DataSourceConfiguration : IEntityTypeConfiguration<DataSource>
{
    public void Configure(EntityTypeBuilder<DataSource> builder)
    {
        builder.ToTable("col_source");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Agency).HasColumnName("agency").HasMaxLength(128).IsRequired();
        builder.Property(x => x.EconomyCode).HasColumnName("economy_code").HasMaxLength(2).IsFixedLength();
        builder.Property(x => x.Type).HasColumnName("type").HasConversion<short>();
        builder.Property(x => x.BaseUrl).HasColumnName("base_url").HasMaxLength(512).IsRequired();
        builder.Property(x => x.RateLimit).HasColumnName("rate_limit");
        builder.Property(x => x.RateLimitUnit).HasColumnName("rate_limit_unit").HasConversion<short>();
        builder.Property(x => x.TimeoutSeconds).HasColumnName("timeout_seconds");
        builder.Property(x => x.CredentialRequired).HasColumnName("credential_required");
        builder.Property(x => x.Credential).HasColumnName("credential");
        builder.Property(x => x.ComplianceClass).HasColumnName("compliance_class").HasConversion<short>();
        builder.Property(x => x.TermsUrl).HasColumnName("terms_url").HasMaxLength(512).IsRequired();
        builder.Property(x => x.RobotsResult).HasColumnName("robots_result").HasConversion<short>();
        builder.Property(x => x.Remark).HasColumnName("remark").HasMaxLength(500);
        builder.Property(x => x.IsEnabled).HasColumnName("is_enabled");
        builder.HasAuditColumns();

        builder.HasIndex(x => x.Name).IsUnique();

        // 删除经济体前检查引用（BR-028）
        builder.HasIndex(x => x.EconomyCode);
    }
}
