using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Sys.Entities;

namespace Suanpan.Data.Configurations.Sys;

public class SysRolePermissionConfiguration : IEntityTypeConfiguration<SysRolePermission>
{
    public void Configure(EntityTypeBuilder<SysRolePermission> builder)
    {
        builder.ToTable("sys_role_permission");

        builder.HasKey(x => new { x.RoleId, x.PermId });
        builder.Property(x => x.RoleId).HasColumnName("role_id");
        builder.Property(x => x.PermId).HasColumnName("perm_id");

        builder.HasIndex(x => x.PermId);
    }
}
