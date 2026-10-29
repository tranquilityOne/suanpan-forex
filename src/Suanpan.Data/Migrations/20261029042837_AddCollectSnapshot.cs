using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Suanpan.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "locked_count",
                table: "col_run",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "parse_output",
                table: "col_run",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "col_snapshot",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    run_id = table.Column<long>(type: "bigint", nullable: false),
                    attempt = table.Column<short>(type: "smallint", nullable: false),
                    seq = table.Column<short>(type: "smallint", nullable: false),
                    source_id = table.Column<long>(type: "bigint", nullable: false),
                    task_id = table.Column<long>(type: "bigint", nullable: false),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    request_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    fetched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    http_status = table.Column<short>(type: "smallint", nullable: false),
                    content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    content_hash = table.Column<string>(type: "char(64)", nullable: false),
                    size = table.Column<int>(type: "integer", nullable: false),
                    unchanged = table.Column<bool>(type: "boolean", nullable: false),
                    is_valid = table.Column<bool>(type: "boolean", nullable: false),
                    invalid_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    referenced = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_col_snapshot", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "col_snapshot_content",
                columns: table => new
                {
                    content_hash = table.Column<string>(type: "char(64)", nullable: false),
                    body = table.Column<byte[]>(type: "bytea", nullable: false),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_col_snapshot_content", x => x.content_hash);
                });

            // 内容已经 gzip 压缩，不让 PostgreSQL 再压缩一次（采集设计 §2.1）
            migrationBuilder.Sql("ALTER TABLE col_snapshot_content ALTER COLUMN body SET STORAGE EXTERNAL;");

            migrationBuilder.CreateIndex(
                name: "ix_col_snapshot_content_hash",
                table: "col_snapshot",
                column: "content_hash");

            migrationBuilder.CreateIndex(
                name: "ix_col_snapshot_fetched_at",
                table: "col_snapshot",
                column: "fetched_at");

            migrationBuilder.CreateIndex(
                name: "ix_col_snapshot_run_id",
                table: "col_snapshot",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "ix_col_snapshot_source_id_fetched_at",
                table: "col_snapshot",
                columns: new[] { "source_id", "fetched_at" });

            migrationBuilder.CreateIndex(
                name: "ix_col_snapshot_source_id_request_key_id",
                table: "col_snapshot",
                columns: new[] { "source_id", "request_key", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_col_snapshot_task_id_fetched_at",
                table: "col_snapshot",
                columns: new[] { "task_id", "fetched_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "col_snapshot");

            migrationBuilder.DropTable(
                name: "col_snapshot_content");

            migrationBuilder.DropColumn(
                name: "locked_count",
                table: "col_run");

            migrationBuilder.DropColumn(
                name: "parse_output",
                table: "col_run");
        }
    }
}
