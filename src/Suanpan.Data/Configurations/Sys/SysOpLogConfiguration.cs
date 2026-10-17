using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Sys.Entities;

namespace Suanpan.Data.Configurations.Sys;

public class SysOpLogConfiguration : IEntityTypeConfiguration<SysOpLog>
{
    public void Configure(EntityTypeBuilder<SysOpLog> builder)
    {
        builder.ToTable("sys_op_log");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Username).HasColumnName("username").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Roles).HasColumnName("roles").HasMaxLength(256).IsRequired();
        builder.Property(x => x.Module).HasColumnName("module").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(64).IsRequired();
        builder.Property(x => x.PermCode).HasColumnName("perm_code").HasMaxLength(128);
        builder.Property(x => x.HttpMethod).HasColumnName("http_method").HasMaxLength(8).IsRequired();
        builder.Property(x => x.RequestUrl).HasColumnName("request_url").HasMaxLength(256).IsRequired();
        builder.Property(x => x.RequestParams).HasColumnName("request_params").HasColumnType("text");
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(512);
        builder.Property(x => x.IsSuccess).HasColumnName("is_success");
        builder.Property(x => x.ErrorMsg).HasColumnName("error_msg").HasMaxLength(512);
        builder.Property(x => x.DurationMs).HasColumnName("duration_ms");
        builder.Property(x => x.Ip).HasColumnName("ip").HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(256);
        builder.Property(x => x.TraceId).HasColumnName("trace_id").HasMaxLength(64);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.PrevHash).HasColumnName("prev_hash").HasMaxLength(64);

        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
        builder.HasIndex(x => new { x.Module, x.Action, x.CreatedAt });
    }
}
