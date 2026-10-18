using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Suanpan.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateSysTablesAndGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 默认授权：迁移账号之后在 public 中新建的表和序列，自动授予应用账号（数据库变更约定 §2.3）。
            // 必须在建表之前执行，且迁移必须用 suanpan_migrator 执行，否则新表不会得到授权。
            migrationBuilder.Sql("""
                ALTER DEFAULT PRIVILEGES FOR ROLE suanpan_migrator IN SCHEMA public
                    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO suanpan_app;
                ALTER DEFAULT PRIVILEGES FOR ROLE suanpan_migrator IN SCHEMA public
                    GRANT USAGE, SELECT ON SEQUENCES TO suanpan_app;
                """);

            // Hangfire 的独立 schema：应用账号可在其中建表，Hangfire 启动时自己建表和升级（数据库变更约定 §2.4）
            migrationBuilder.Sql("""
                CREATE SCHEMA IF NOT EXISTS hangfire;
                GRANT USAGE, CREATE ON SCHEMA hangfire TO suanpan_app;
                """);

            migrationBuilder.CreateTable(
                name: "sys_config",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    config_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    config_value = table.Column<string>(type: "text", nullable: false),
                    value_type = table.Column<short>(type: "smallint", nullable: false),
                    config_group = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_config", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sys_ip_whitelist",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ip_value = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    remark = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_ip_whitelist", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sys_login_log",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: true),
                    stage = table.Column<short>(type: "smallint", nullable: false),
                    is_success = table.Column<bool>(type: "boolean", nullable: false),
                    fail_reason = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_login_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sys_op_log",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    roles = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    module = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    perm_code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    http_method = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    request_url = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    request_params = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    is_success = table.Column<bool>(type: "boolean", nullable: false),
                    error_msg = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: false),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    trace_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    prev_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_op_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sys_permission",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    parent_id = table.Column<long>(type: "bigint", nullable: false),
                    perm_type = table.Column<short>(type: "smallint", nullable: false),
                    perm_code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    perm_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    menu_url = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    icon = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    sort = table.Column<int>(type: "integer", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_permission", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sys_role",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'100', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    role_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_builtin = table.Column<bool>(type: "boolean", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    remark = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_role", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sys_role_permission",
                columns: table => new
                {
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    perm_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_role_permission", x => new { x.role_id, x.perm_id });
                });

            migrationBuilder.CreateTable(
                name: "sys_user",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    real_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    email = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    must_change_password = table.Column<bool>(type: "boolean", nullable: false),
                    totp_secret = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    security_stamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    login_fail_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    locked_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_login_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_user", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sys_user_role",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_user_role", x => new { x.user_id, x.role_id });
                });

            migrationBuilder.InsertData(
                table: "sys_role",
                columns: new[] { "id", "created_at", "created_by", "is_builtin", "is_enabled", "remark", "role_code", "role_name", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { 1L, new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "migration", true, true, null, "super_admin", "管理员", null, null },
                    { 2L, new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "migration", false, true, null, "engineer", "采集工程师", null, null },
                    { 3L, new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "migration", false, true, null, "editor", "编辑", null, null },
                    { 4L, new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "migration", false, true, null, "viewer", "只读", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "ux_sys_config_config_key",
                table: "sys_config",
                column: "config_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_sys_ip_whitelist_ip_value",
                table: "sys_ip_whitelist",
                column: "ip_value",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sys_login_log_created_at",
                table: "sys_login_log",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_sys_login_log_username_created_at",
                table: "sys_login_log",
                columns: new[] { "username", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sys_op_log_created_at",
                table: "sys_op_log",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_sys_op_log_module_action_created_at",
                table: "sys_op_log",
                columns: new[] { "module", "action", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sys_op_log_user_id_created_at",
                table: "sys_op_log",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sys_permission_parent_id",
                table: "sys_permission",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ux_sys_permission_perm_code",
                table: "sys_permission",
                column: "perm_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_sys_role_role_code",
                table: "sys_role",
                column: "role_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sys_role_permission_perm_id",
                table: "sys_role_permission",
                column: "perm_id");

            migrationBuilder.CreateIndex(
                name: "ux_sys_user_username",
                table: "sys_user",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sys_user_role_role_id",
                table: "sys_user_role",
                column: "role_id");

            // 两张日志表只追加：应用账号只保留 INSERT、SELECT（BR-148）
            migrationBuilder.Sql("REVOKE UPDATE, DELETE ON sys_op_log, sys_login_log FROM suanpan_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("GRANT UPDATE, DELETE ON sys_op_log, sys_login_log TO suanpan_app;");

            migrationBuilder.DropTable(
                name: "sys_config");

            migrationBuilder.DropTable(
                name: "sys_ip_whitelist");

            migrationBuilder.DropTable(
                name: "sys_login_log");

            migrationBuilder.DropTable(
                name: "sys_op_log");

            migrationBuilder.DropTable(
                name: "sys_permission");

            migrationBuilder.DropTable(
                name: "sys_role");

            migrationBuilder.DropTable(
                name: "sys_role_permission");

            migrationBuilder.DropTable(
                name: "sys_user");

            migrationBuilder.DropTable(
                name: "sys_user_role");

            // 会一并删除 Hangfire 在其中建的全部表
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS hangfire CASCADE;");

            migrationBuilder.Sql("""
                ALTER DEFAULT PRIVILEGES FOR ROLE suanpan_migrator IN SCHEMA public
                    REVOKE SELECT, INSERT, UPDATE, DELETE ON TABLES FROM suanpan_app;
                ALTER DEFAULT PRIVILEGES FOR ROLE suanpan_migrator IN SCHEMA public
                    REVOKE USAGE, SELECT ON SEQUENCES FROM suanpan_app;
                """);
        }
    }
}
