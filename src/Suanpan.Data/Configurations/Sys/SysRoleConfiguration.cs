using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Sys.Entities;
using Suanpan.Data.Configurations.Common;

namespace Suanpan.Data.Configurations.Sys;

public class SysRoleConfiguration : IEntityTypeConfiguration<SysRole>
{
    public void Configure(EntityTypeBuilder<SysRole> builder)
    {
        builder.ToTable("sys_role");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.RoleCode).HasColumnName("role_code").HasMaxLength(64).IsRequired();
        builder.Property(x => x.RoleName).HasColumnName("role_name").HasMaxLength(64).IsRequired();
        builder.Property(x => x.IsBuiltin).HasColumnName("is_builtin");
        builder.Property(x => x.IsEnabled).HasColumnName("is_enabled");
        builder.Property(x => x.Remark).HasColumnName("remark").HasMaxLength(256);
        builder.HasAuditColumns();

        builder.HasIndex(x => x.RoleCode).IsUnique();
    }
}
