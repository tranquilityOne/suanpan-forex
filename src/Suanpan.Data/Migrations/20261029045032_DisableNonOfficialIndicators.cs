using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Suanpan.Data.Migrations
{
    /// <summary>
    /// 停用 7 个非政府机构发布的指标（PRD Q-004、DEC-024；首批指标清单 §2 第 9 项；JAN-87）：排期只来自数据源，
    /// 这些机构没有可采集的官方日程，暂不纳入日历，法务确认授权后（v1.1）在后台重新启用。
    /// 只执行一次的数据迁移：已经停用的不动；以后在后台启停，不再改这个迁移（数据库变更约定 §3.4）
    /// </summary>
    public partial class DisableNonOfficialIndicators : Migration
    {
        private const string UpdatedBy = "migration";
        private const string UpdatedAt = "2026-10-29 00:00:00+00";

        private static readonly string[] Codes =
        [
            "US.ISM.MFG", "US.ISM.SVC", "US.UMCSI.PRELIM", "EA.PMI.MFG.PRELIM", "EA.PMI.SVC.PRELIM", "DE.IFO", "DE.ZEW",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                UPDATE ref_indicator SET is_enabled = false, updated_by = '{UpdatedBy}', updated_at = '{UpdatedAt}'
                WHERE is_enabled AND code IN ({CodeList()});
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 只恢复本迁移停用、之后没有在后台改过的行
            migrationBuilder.Sql($"""
                UPDATE ref_indicator SET is_enabled = true
                WHERE NOT is_enabled AND updated_by = '{UpdatedBy}' AND updated_at = '{UpdatedAt}' AND code IN ({CodeList()});
                """);
        }

        private static string CodeList() => string.Join(", ", Codes.Select(code => $"'{code}'"));
    }
}
