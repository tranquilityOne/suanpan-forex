using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Sys.Entities;
using Suanpan.Data.Configurations.Common;

namespace Suanpan.Data.Configurations.Sys;

public class SysIpWhitelistConfiguration : IEntityTypeConfiguration<SysIpWhitelist>
{
    public void Configure(EntityTypeBuilder<SysIpWhitelist> builder)
    {
        builder.ToTable("sys_ip_whitelist");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.IpValue).HasColumnName("ip_value").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Remark).HasColumnName("remark").HasMaxLength(128);
        builder.Property(x => x.IsEnabled).HasColumnName("is_enabled");
        builder.HasAuditColumns();

        builder.HasIndex(x => x.IpValue).IsUnique();
    }
}
