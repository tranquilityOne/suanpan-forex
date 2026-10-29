using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Suanpan.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cal_event",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    indicator_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reference_period = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    origin = table.Column<short>(type: "smallint", nullable: false),
                    release_batch_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    schedule_source_id = table.Column<long>(type: "bigint", nullable: true),
                    schedule_release_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    scheduled_local = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    scheduled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    time_precision = table.Column<short>(type: "smallint", nullable: false),
                    local_time_resolution = table.Column<short>(type: "smallint", nullable: false),
                    actual = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    actual_upper = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    actual_raw = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    actual_source_id = table.Column<long>(type: "bigint", nullable: true),
                    detected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    source_snapshot_id = table.Column<long>(type: "bigint", nullable: true),
                    forecast = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    forecast_upper = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    forecast_type = table.Column<short>(type: "smallint", nullable: true),
                    forecast_note = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    previous = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    previous_upper = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    previous_revised = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    revised_value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    locked_fields = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cal_event", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cal_event_change",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    event_id = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<short>(type: "smallint", nullable: false),
                    old_value = table.Column<string>(type: "jsonb", nullable: true),
                    new_value = table.Column<string>(type: "jsonb", nullable: true),
                    actor_kind = table.Column<short>(type: "smallint", nullable: false),
                    source_id = table.Column<long>(type: "bigint", nullable: true),
                    run_id = table.Column<long>(type: "bigint", nullable: true),
                    changed_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cal_event_change", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cal_event_value",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    event_id = table.Column<long>(type: "bigint", nullable: false),
                    source_id = table.Column<long>(type: "bigint", nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    value_upper = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    raw = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    snapshot_id = table.Column<long>(type: "bigint", nullable: true),
                    run_id = table.Column<long>(type: "bigint", nullable: false),
                    detected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cal_event_value", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cal_ignored_release",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    source_id = table.Column<long>(type: "bigint", nullable: false),
                    release_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    release_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cal_ignored_release", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cal_lock_conflict",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    event_id = table.Column<long>(type: "bigint", nullable: false),
                    field = table.Column<short>(type: "smallint", nullable: false),
                    locked_value = table.Column<string>(type: "jsonb", nullable: false),
                    collected_value = table.Column<string>(type: "jsonb", nullable: false),
                    source_id = table.Column<long>(type: "bigint", nullable: false),
                    snapshot_id = table.Column<long>(type: "bigint", nullable: true),
                    run_id = table.Column<long>(type: "bigint", nullable: false),
                    first_detected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_detected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    resolved_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolution = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cal_lock_conflict", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cal_unmatched_entry",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    source_id = table.Column<long>(type: "bigint", nullable: false),
                    release_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    release_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    reference_period = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: true),
                    local_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    time_precision = table.Column<short>(type: "smallint", nullable: false),
                    first_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_run_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cal_unmatched_entry", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cal_event_actual_source_id",
                table: "cal_event",
                column: "actual_source_id");

            migrationBuilder.CreateIndex(
                name: "ix_cal_event_release_batch_id",
                table: "cal_event",
                column: "release_batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_cal_event_schedule_source_id_schedule_release_key",
                table: "cal_event",
                columns: new[] { "schedule_source_id", "schedule_release_key" });

            migrationBuilder.CreateIndex(
                name: "ix_cal_event_scheduled_at",
                table: "cal_event",
                column: "scheduled_at");

            migrationBuilder.CreateIndex(
                name: "ix_cal_event_status",
                table: "cal_event",
                column: "status",
                filter: "status IN (4, 5, 6)");

            migrationBuilder.CreateIndex(
                name: "ux_cal_event_indicator_code_reference_period",
                table: "cal_event",
                columns: new[] { "indicator_code", "reference_period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cal_event_change_event_id",
                table: "cal_event_change",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_cal_event_value_source_id",
                table: "cal_event_value",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "ux_cal_event_value_event_id_source_id",
                table: "cal_event_value",
                columns: new[] { "event_id", "source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_cal_ignored_release_source_id_release_key",
                table: "cal_ignored_release",
                columns: new[] { "source_id", "release_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cal_lock_conflict_source_id",
                table: "cal_lock_conflict",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "ix_cal_lock_conflict_status",
                table: "cal_lock_conflict",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_cal_lock_conflict_event_id_field",
                table: "cal_lock_conflict",
                columns: new[] { "event_id", "field" },
                unique: true,
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "ux_cal_unmatched_entry_source_id_release_key_reference_period",
                table: "cal_unmatched_entry",
                columns: new[] { "source_id", "release_key", "reference_period" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cal_event");

            migrationBuilder.DropTable(
                name: "cal_event_change");

            migrationBuilder.DropTable(
                name: "cal_event_value");

            migrationBuilder.DropTable(
                name: "cal_ignored_release");

            migrationBuilder.DropTable(
                name: "cal_lock_conflict");

            migrationBuilder.DropTable(
                name: "cal_unmatched_entry");
        }
    }
}
