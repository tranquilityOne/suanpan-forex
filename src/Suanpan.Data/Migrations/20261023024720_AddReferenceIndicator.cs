using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Suanpan.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceIndicator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ref_indicator",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    economy_code = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    category = table.Column<short>(type: "smallint", nullable: false),
                    value_type = table.Column<short>(type: "smallint", nullable: false),
                    unit = table.Column<short>(type: "smallint", nullable: false),
                    decimals = table.Column<short>(type: "smallint", nullable: false),
                    frequency = table.Column<short>(type: "smallint", nullable: false),
                    importance = table.Column<short>(type: "smallint", nullable: false),
                    direction = table.Column<short>(type: "smallint", nullable: false),
                    flat_tolerance = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    range_min = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    range_max = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    sigma_n = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    default_release_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    agency_en = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    agency_zh = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description_en = table.Column<string>(type: "text", nullable: true),
                    description_zh = table.Column<string>(type: "text", nullable: true),
                    name_en = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name_zh = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    short_name_en = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    short_name_zh = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ref_indicator", x => x.code);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ref_indicator_currency_code",
                table: "ref_indicator",
                column: "currency_code");

            migrationBuilder.CreateIndex(
                name: "ix_ref_indicator_economy_code",
                table: "ref_indicator",
                column: "economy_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ref_indicator");
        }
    }
}
