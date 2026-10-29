using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Suanpan.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectRunReparse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "heartbeat_at",
                table: "col_run",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reparse",
                table: "col_run",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "heartbeat_at",
                table: "col_run");

            migrationBuilder.DropColumn(
                name: "reparse",
                table: "col_run");
        }
    }
}
