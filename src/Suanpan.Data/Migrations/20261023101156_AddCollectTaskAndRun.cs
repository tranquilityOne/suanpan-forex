using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Suanpan.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectTaskAndRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "col_run",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    task_id = table.Column<long>(type: "bigint", nullable: false),
                    source_id = table.Column<long>(type: "bigint", nullable: false),
                    trigger = table.Column<short>(type: "smallint", nullable: false),
                    scheduled_for = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    triggered_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    queued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<short>(type: "smallint", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    inserted_count = table.Column<int>(type: "integer", nullable: false),
                    updated_count = table.Column<int>(type: "integer", nullable: false),
                    skipped_count = table.Column<int>(type: "integer", nullable: false),
                    error_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    trace_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    attempts = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_col_run", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "col_task",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_id = table.Column<long>(type: "bigint", nullable: false),
                    parser_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    target_type = table.Column<short>(type: "smallint", nullable: false),
                    cron = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    @params = table.Column<string>(name: "params", type: "jsonb", nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    last_dispatched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_col_task", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_col_run_queued_at",
                table: "col_run",
                column: "queued_at");

            migrationBuilder.CreateIndex(
                name: "ix_col_run_source_id_queued_at",
                table: "col_run",
                columns: new[] { "source_id", "queued_at" });

            migrationBuilder.CreateIndex(
                name: "ix_col_run_status",
                table: "col_run",
                column: "status",
                filter: "status IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "ix_col_run_task_id_queued_at",
                table: "col_run",
                columns: new[] { "task_id", "queued_at" });

            migrationBuilder.CreateIndex(
                name: "ux_col_run_task_id_scheduled_for",
                table: "col_run",
                columns: new[] { "task_id", "scheduled_for" },
                unique: true,
                filter: "scheduled_for IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_col_task_source_id",
                table: "col_task",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "ux_col_task_name",
                table: "col_task",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "col_run");

            migrationBuilder.DropTable(
                name: "col_task");
        }
    }
}
