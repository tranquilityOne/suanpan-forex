using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Sys.Entities;

namespace Suanpan.Data.Configurations.Sys;

public class SysLoginLogConfiguration : IEntityTypeConfiguration<SysLoginLog>
{
    public void Configure(EntityTypeBuilder<SysLoginLog> builder)
    {
        builder.ToTable("sys_login_log");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.Username).HasColumnName("username").HasMaxLength(64).IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Stage).HasColumnName("stage").HasConversion<short>();
        builder.Property(x => x.IsSuccess).HasColumnName("is_success");
        builder.Property(x => x.FailReason).HasColumnName("fail_reason").HasMaxLength(128);
        builder.Property(x => x.Ip).HasColumnName("ip").HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(256);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.Username, x.CreatedAt });
    }
}
