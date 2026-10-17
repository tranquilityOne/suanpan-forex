using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Sys.Entities;
using Suanpan.Data.Configurations.Common;

namespace Suanpan.Data.Configurations.Sys;

public class SysUserConfiguration : IEntityTypeConfiguration<SysUser>
{
    public void Configure(EntityTypeBuilder<SysUser> builder)
    {
        builder.ToTable("sys_user");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.Username).HasColumnName("username").HasMaxLength(64).IsRequired();
        builder.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(256).IsRequired();
        builder.Property(x => x.RealName).HasColumnName("real_name").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(128);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<short>();
        builder.Property(x => x.MustChangePassword).HasColumnName("must_change_password");
        builder.Property(x => x.TotpSecret).HasColumnName("totp_secret").HasMaxLength(256);
        builder.Property(x => x.SecurityStamp).HasColumnName("security_stamp").HasMaxLength(64).IsRequired();
        builder.Property(x => x.LoginFailCount).HasColumnName("login_fail_count").HasDefaultValue(0);
        builder.Property(x => x.LockedUntil).HasColumnName("locked_until");
        builder.Property(x => x.LastLoginAt).HasColumnName("last_login_at");
        builder.Property(x => x.LastLoginIp).HasColumnName("last_login_ip").HasMaxLength(64);
        builder.HasAuditColumns();

        builder.HasIndex(x => x.Username).IsUnique();
    }
}
