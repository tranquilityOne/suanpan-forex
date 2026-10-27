using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Suanpan.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceIndicatorSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ref_indicator_source",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    indicator_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<long>(type: "bigint", nullable: false),
                    external_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    parser_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    priority = table.Column<short>(type: "smallint", nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ref_indicator_source", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ref_indicator_source_source_id",
                table: "ref_indicator_source",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "ux_ref_indicator_source_indicator_code_priority",
                table: "ref_indicator_source",
                columns: new[] { "indicator_code", "priority" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_ref_indicator_source_indicator_code_source_id",
                table: "ref_indicator_source",
                columns: new[] { "indicator_code", "source_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ref_indicator_source");
        }
    }
}
