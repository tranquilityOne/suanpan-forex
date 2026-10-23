using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Suanpan.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "col_source",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    agency = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    economy_code = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true),
                    type = table.Column<short>(type: "smallint", nullable: false),
                    base_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    rate_limit = table.Column<int>(type: "integer", nullable: false),
                    rate_limit_unit = table.Column<short>(type: "smallint", nullable: false),
                    timeout_seconds = table.Column<short>(type: "smallint", nullable: false),
                    credential_required = table.Column<bool>(type: "boolean", nullable: false),
                    credential = table.Column<string>(type: "text", nullable: true),
                    compliance_class = table.Column<short>(type: "smallint", nullable: false),
                    terms_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    robots_result = table.Column<short>(type: "smallint", nullable: false),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_col_source", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_col_source_economy_code",
                table: "col_source",
                column: "economy_code");

            migrationBuilder.CreateIndex(
                name: "ux_col_source_name",
                table: "col_source",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "col_source");
        }
    }
}
