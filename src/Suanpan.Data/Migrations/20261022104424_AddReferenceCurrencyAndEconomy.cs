using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Suanpan.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceCurrencyAndEconomy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ref_currency",
                columns: table => new
                {
                    code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    name_en = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_zh = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ref_currency", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "ref_economy",
                columns: table => new
                {
                    code = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    timezone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sort = table.Column<short>(type: "smallint", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    name_en = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_zh = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ref_economy", x => x.code);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ref_economy_currency_code",
                table: "ref_economy",
                column: "currency_code");

            // 预置数据（基础数据设计 §2.4）手写在这里：HasData 会丢掉复合类型（中英文文本）的值。
            // 审计字段按数据库变更约定 §3.4：created_by 为 migration，created_at 为固定时间
            var seedTime = new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc);

            migrationBuilder.InsertData(
                table: "ref_currency",
                columns: ["code", "name_zh", "name_en", "is_enabled", "created_by", "created_at"],
                values: new object[,]
                {
                    { "USD", "美元", "US Dollar", true, "migration", seedTime },
                    { "EUR", "欧元", "Euro", true, "migration", seedTime },
                    { "GBP", "英镑", "British Pound", true, "migration", seedTime },
                    { "JPY", "日元", "Japanese Yen", true, "migration", seedTime },
                    { "CNY", "人民币", "Chinese Yuan", true, "migration", seedTime },
                    { "CHF", "瑞士法郎", "Swiss Franc", true, "migration", seedTime },
                    { "CAD", "加元", "Canadian Dollar", true, "migration", seedTime },
                    { "AUD", "澳元", "Australian Dollar", true, "migration", seedTime },
                    { "NZD", "新西兰元", "New Zealand Dollar", true, "migration", seedTime },
                });

            // 德国单列为 DE（PRD Q-008）
            migrationBuilder.InsertData(
                table: "ref_economy",
                columns: ["code", "name_zh", "name_en", "currency_code", "timezone", "sort", "is_enabled", "created_by", "created_at"],
                values: new object[,]
                {
                    { "US", "美国", "United States", "USD", "America/New_York", (short)10, true, "migration", seedTime },
                    { "EA", "欧元区", "Euro Area", "EUR", "Europe/Brussels", (short)20, true, "migration", seedTime },
                    { "DE", "德国", "Germany", "EUR", "Europe/Berlin", (short)30, true, "migration", seedTime },
                    { "GB", "英国", "United Kingdom", "GBP", "Europe/London", (short)40, true, "migration", seedTime },
                    { "JP", "日本", "Japan", "JPY", "Asia/Tokyo", (short)50, true, "migration", seedTime },
                    { "CN", "中国", "China", "CNY", "Asia/Shanghai", (short)60, true, "migration", seedTime },
                    { "CH", "瑞士", "Switzerland", "CHF", "Europe/Zurich", (short)70, true, "migration", seedTime },
                    { "CA", "加拿大", "Canada", "CAD", "America/Toronto", (short)80, true, "migration", seedTime },
                    { "AU", "澳大利亚", "Australia", "AUD", "Australia/Sydney", (short)90, true, "migration", seedTime },
                    { "NZ", "新西兰", "New Zealand", "NZD", "Pacific/Auckland", (short)100, true, "migration", seedTime },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ref_currency");

            migrationBuilder.DropTable(
                name: "ref_economy");
        }
    }
}
