using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Sys.Entities;
using Suanpan.Data.Configurations.Common;

namespace Suanpan.Data.Configurations.Sys;

public class SysPermissionConfiguration : IEntityTypeConfiguration<SysPermission>
{
    public void Configure(EntityTypeBuilder<SysPermission> builder)
    {
        builder.ToTable("sys_permission");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.ParentId).HasColumnName("parent_id");
        builder.Property(x => x.PermType).HasColumnName("perm_type").HasConversion<short>();
        builder.Property(x => x.PermCode).HasColumnName("perm_code").HasMaxLength(128).IsRequired();
        builder.Property(x => x.PermName).HasColumnName("perm_name").HasMaxLength(64).IsRequired();
        builder.Property(x => x.MenuUrl).HasColumnName("menu_url").HasMaxLength(256);
        builder.Property(x => x.Icon).HasColumnName("icon").HasMaxLength(64);
        builder.Property(x => x.Sort).HasColumnName("sort");
        builder.Property(x => x.IsEnabled).HasColumnName("is_enabled");
        builder.HasAuditColumns();

        builder.HasIndex(x => x.PermCode).IsUnique();
        builder.HasIndex(x => x.ParentId);
    }
}
