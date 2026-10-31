using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Suanpan.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropRunTaskScheduledIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_col_run_task_id_scheduled_for",
                table: "col_run");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_col_run_task_id_scheduled_for",
                table: "col_run",
                columns: new[] { "task_id", "scheduled_for" },
                unique: true,
                filter: "scheduled_for IS NOT NULL");
        }
    }
}
