using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suanpan.Core.Sys.Constants;
using Suanpan.Core.Sys.Entities;
using Suanpan.Data.Configurations.Common;

namespace Suanpan.Data.Configurations.Sys;

public class SysRoleConfiguration : IEntityTypeConfiguration<SysRole>
{
    // 预置数据的审计字段：固定值，否则每次构建模型都会产生新的迁移
    private const string SeedActor = "migration";
    private static readonly DateTime SeedTime = new(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<SysRole> builder)
    {
        builder.ToTable("sys_role");

        builder.HasKey(x => x.Id);
        // id 1 ~ 99 留给预置角色，后台新建的角色从 100 开始（后端约定 §6.2）
        builder.Property(x => x.Id).HasColumnName("id").HasIdentityOptions(startValue: 100);

        builder.Property(x => x.RoleCode).HasColumnName("role_code").HasMaxLength(64).IsRequired();
        builder.Property(x => x.RoleName).HasColumnName("role_name").HasMaxLength(64).IsRequired();
        builder.Property(x => x.IsBuiltin).HasColumnName("is_builtin");
        builder.Property(x => x.IsEnabled).HasColumnName("is_enabled");
        builder.Property(x => x.Remark).HasColumnName("remark").HasMaxLength(256);
        builder.HasAuditColumns();

        builder.HasIndex(x => x.RoleCode).IsUnique();

        // 预置角色（sys 设计 §3.4）：只有管理员是内置角色，其余 3 个的权限可以在后台调整
        builder.HasData(
            Preset(1, RoleCodes.SuperAdmin, "管理员", isBuiltin: true),
            Preset(2, RoleCodes.Engineer, "采集工程师", isBuiltin: false),
            Preset(3, RoleCodes.Editor, "编辑", isBuiltin: false),
            Preset(4, RoleCodes.Viewer, "只读", isBuiltin: false));
    }

    private static object Preset(long id, string roleCode, string roleName, bool isBuiltin) => new
    {
        Id = id,
        RoleCode = roleCode,
        RoleName = roleName,
        IsBuiltin = isBuiltin,
        IsEnabled = true,
        CreatedBy = SeedActor,
        CreatedAt = SeedTime,
    };
}
