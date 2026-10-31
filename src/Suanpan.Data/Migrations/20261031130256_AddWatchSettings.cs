using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Suanpan.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "watch_lead_seconds",
                table: "ref_indicator_source",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "watch_window_minutes",
                table: "ref_indicator_source",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "daily_limit",
                table: "col_source",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "watch_interval_seconds",
                table: "col_source",
                type: "smallint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_col_run_task_id_trigger_scheduled_for",
                table: "col_run",
                columns: new[] { "task_id", "trigger", "scheduled_for" },
                unique: true,
                filter: "scheduled_for IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_col_run_task_id_trigger_scheduled_for",
                table: "col_run");

            migrationBuilder.DropColumn(
                name: "watch_lead_seconds",
                table: "ref_indicator_source");

            migrationBuilder.DropColumn(
                name: "watch_window_minutes",
                table: "ref_indicator_source");

            migrationBuilder.DropColumn(
                name: "daily_limit",
                table: "col_source");

            migrationBuilder.DropColumn(
                name: "watch_interval_seconds",
                table: "col_source");
        }
    }
}
