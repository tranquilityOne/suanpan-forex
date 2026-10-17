using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Sys.Entities;
using Suanpan.Data.Configurations.Common;

namespace Suanpan.Data.Configurations.Sys;

public class SysUserRoleConfiguration : IEntityTypeConfiguration<SysUserRole>
{
    public void Configure(EntityTypeBuilder<SysUserRole> builder)
    {
        builder.ToTable("sys_user_role");

        builder.HasKey(x => new { x.UserId, x.RoleId });
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.RoleId).HasColumnName("role_id");
        builder.HasCreationAuditColumns();

        builder.HasIndex(x => x.RoleId);
    }
}
