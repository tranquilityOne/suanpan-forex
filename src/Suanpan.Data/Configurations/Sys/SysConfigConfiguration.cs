using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Sys.Entities;
using Suanpan.Data.Configurations.Common;

namespace Suanpan.Data.Configurations.Sys;

public class SysConfigConfiguration : IEntityTypeConfiguration<SysConfig>
{
    public void Configure(EntityTypeBuilder<SysConfig> builder)
    {
        builder.ToTable("sys_config");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.ConfigKey).HasColumnName("config_key").HasMaxLength(128).IsRequired();
        builder.Property(x => x.ConfigValue).HasColumnName("config_value").HasColumnType("text").IsRequired();
        builder.Property(x => x.ValueType).HasColumnName("value_type").HasConversion<short>();
        builder.Property(x => x.ConfigGroup).HasColumnName("config_group").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(256);
        builder.HasAuditColumns();

        builder.HasIndex(x => x.ConfigKey).IsUnique();
    }
}
