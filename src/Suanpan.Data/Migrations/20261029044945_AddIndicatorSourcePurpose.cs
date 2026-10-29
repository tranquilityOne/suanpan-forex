using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Suanpan.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIndicatorSourcePurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ref_indicator_source_source_id",
                table: "ref_indicator_source");

            migrationBuilder.DropIndex(
                name: "ux_ref_indicator_source_indicator_code_priority",
                table: "ref_indicator_source");

            migrationBuilder.DropIndex(
                name: "ux_ref_indicator_source_indicator_code_source_id",
                table: "ref_indicator_source");

            migrationBuilder.AlterColumn<short>(
                name: "priority",
                table: "ref_indicator_source",
                type: "smallint",
                nullable: true,
                oldClrType: typeof(short),
                oldType: "smallint");

            // 已有的映射都是公布值（基础数据设计 §2.1）。默认值保留在数据库中：上一版程序新增映射时不写这一列，
            // 同样记为公布值，两版程序可以同时运行（数据库变更约定 §5）；模型中不声明默认值，程序总是写明用途
            migrationBuilder.AddColumn<short>(
                name: "purpose",
                table: "ref_indicator_source",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1);

            migrationBuilder.CreateIndex(
                name: "ix_ref_indicator_source_source_id_purpose_external_id",
                table: "ref_indicator_source",
                columns: new[] { "source_id", "purpose", "external_id" });

            migrationBuilder.CreateIndex(
                name: "ux_ref_indicator_source_indicator_code_priority",
                table: "ref_indicator_source",
                columns: new[] { "indicator_code", "priority" },
                unique: true,
                filter: "purpose = 1");

            migrationBuilder.CreateIndex(
                name: "ux_ref_indicator_source_indicator_code_source_id_purpose",
                table: "ref_indicator_source",
                columns: new[] { "indicator_code", "source_id", "purpose" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ref_indicator_source_source_id_purpose_external_id",
                table: "ref_indicator_source");

            migrationBuilder.DropIndex(
                name: "ux_ref_indicator_source_indicator_code_priority",
                table: "ref_indicator_source");

            migrationBuilder.DropIndex(
                name: "ux_ref_indicator_source_indicator_code_source_id_purpose",
                table: "ref_indicator_source");

            migrationBuilder.DropColumn(
                name: "purpose",
                table: "ref_indicator_source");

            migrationBuilder.AlterColumn<short>(
                name: "priority",
                table: "ref_indicator_source",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldNullable: true);

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
    }
}
