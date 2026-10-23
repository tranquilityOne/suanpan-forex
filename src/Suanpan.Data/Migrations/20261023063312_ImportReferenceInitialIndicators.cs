using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Suanpan.Core.Reference.Enums;

#nullable disable

namespace Suanpan.Data.Migrations
{
    /// <summary>
    /// 首批 50 个指标（docs/design/reference-indicators.md v0.2，已确认；JAN-78）。只执行一次的数据迁移：编码已存在时跳过，
    /// 不覆盖后台的修改；以后的增改在后台进行，不再改这个迁移（数据库变更约定 §3.4）。
    /// 重要性都是"高"，持平容差 0，合理上下限、标准差倍数和说明不填，由编辑在后台补充。
    /// 枚举按值写入，枚举已有的值不能改（基础数据设计 §3.6）。
    /// </summary>
    public partial class ImportReferenceInitialIndicators : Migration
    {
        private const string CreatedBy = "migration";
        private const string CreatedAt = "2026-10-23 00:00:00+00";

        private static readonly Row[] Indicators =
        [
            new("US.RATE", "USD", "美联储利率决议（联邦基金利率目标区间）", "Fed Interest Rate Decision (Federal Funds Target Range)", "美联储利率决议", "Fed Rate Decision",
                IndicatorCategory.InterestRate, IndicatorValueType.Range, IndicatorUnit.Percent, 2, ReleaseFrequency.Irregular, ImpactDirection.HigherIsBullish,
                "美联储", "Federal Reserve", "14:00"),
            new("US.NFP", "USD", "非农就业人数变化", "Nonfarm Payrolls", "非农就业人数", "Nonfarm Payrolls",
                IndicatorCategory.Employment, IndicatorValueType.Value, IndicatorUnit.ThousandPersons, 0, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "美国劳工统计局", "Bureau of Labor Statistics", "08:30"),
            new("US.UNEMP", "USD", "失业率", "Unemployment Rate", "失业率", "Unemployment Rate",
                IndicatorCategory.Employment, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBearish,
                "美国劳工统计局", "Bureau of Labor Statistics", "08:30"),
            new("US.AHE.MOM", "USD", "平均时薪月率", "Average Hourly Earnings MoM", "平均时薪月率", "Avg Hourly Earnings MoM",
                IndicatorCategory.Employment, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "美国劳工统计局", "Bureau of Labor Statistics", "08:30"),
            new("US.CPI.YOY", "USD", "CPI 年率", "CPI YoY", "CPI 年率", "CPI YoY",
                IndicatorCategory.Inflation, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "美国劳工统计局", "Bureau of Labor Statistics", "08:30"),
            new("US.CPI.MOM", "USD", "CPI 月率", "CPI MoM", "CPI 月率", "CPI MoM",
                IndicatorCategory.Inflation, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "美国劳工统计局", "Bureau of Labor Statistics", "08:30"),
            new("US.CORECPI.YOY", "USD", "核心 CPI 年率", "Core CPI YoY", "核心 CPI 年率", "Core CPI YoY",
                IndicatorCategory.Inflation, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "美国劳工统计局", "Bureau of Labor Statistics", "08:30"),
            new("US.CORECPI.MOM", "USD", "核心 CPI 月率", "Core CPI MoM", "核心 CPI 月率", "Core CPI MoM",
                IndicatorCategory.Inflation, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "美国劳工统计局", "Bureau of Labor Statistics", "08:30"),
            new("US.PPI.MOM", "USD", "PPI 月率", "PPI MoM", "PPI 月率", "PPI MoM",
                IndicatorCategory.Inflation, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "美国劳工统计局", "Bureau of Labor Statistics", "08:30"),
            new("US.COREPCE.YOY", "USD", "核心 PCE 物价指数年率", "Core PCE Price Index YoY", "核心 PCE 年率", "Core PCE YoY",
                IndicatorCategory.Inflation, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "美国经济分析局", "Bureau of Economic Analysis", "08:30"),
            new("US.COREPCE.MOM", "USD", "核心 PCE 物价指数月率", "Core PCE Price Index MoM", "核心 PCE 月率", "Core PCE MoM",
                IndicatorCategory.Inflation, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "美国经济分析局", "Bureau of Economic Analysis", "08:30"),
            new("US.GDP.QOQ.PRELIM", "USD", "GDP 年化季率（初值）", "GDP Annualized QoQ (Advance)", "GDP 季率初值", "GDP QoQ Advance",
                IndicatorCategory.Growth, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Quarterly, ImpactDirection.HigherIsBullish,
                "美国经济分析局", "Bureau of Economic Analysis", "08:30"),
            new("US.RETAIL.MOM", "USD", "零售销售月率", "Retail Sales MoM", "零售销售月率", "Retail Sales MoM",
                IndicatorCategory.Consumption, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "美国人口普查局", "U.S. Census Bureau", "08:30"),
            new("US.CLAIMS", "USD", "初请失业金人数", "Initial Jobless Claims", "初请失业金人数", "Initial Jobless Claims",
                IndicatorCategory.Employment, IndicatorValueType.Value, IndicatorUnit.ThousandPersons, 0, ReleaseFrequency.Weekly, ImpactDirection.HigherIsBearish,
                "美国劳工部", "U.S. Department of Labor", "08:30"),
            new("US.JOLTS", "USD", "JOLTS 职位空缺", "JOLTS Job Openings", "JOLTS 职位空缺", "JOLTS Job Openings",
                IndicatorCategory.Employment, IndicatorValueType.Value, IndicatorUnit.ThousandUnits, 0, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "美国劳工统计局", "Bureau of Labor Statistics", "10:00"),
            new("US.ISM.MFG", "USD", "ISM 制造业 PMI", "ISM Manufacturing PMI", "ISM 制造业 PMI", "ISM Manufacturing PMI",
                IndicatorCategory.Sentiment, IndicatorValueType.Value, IndicatorUnit.Index, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "美国供应管理协会", "Institute for Supply Management", "10:00"),
            new("US.ISM.SVC", "USD", "ISM 非制造业 PMI", "ISM Services PMI", "ISM 非制造业 PMI", "ISM Services PMI",
                IndicatorCategory.Sentiment, IndicatorValueType.Value, IndicatorUnit.Index, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "美国供应管理协会", "Institute for Supply Management", "10:00"),
            new("US.UMCSI.PRELIM", "USD", "密歇根大学消费者信心指数（初值）", "University of Michigan Consumer Sentiment (Preliminary)", "密歇根消费者信心初值", "UMich Sentiment Prelim",
                IndicatorCategory.Sentiment, IndicatorValueType.Value, IndicatorUnit.Index, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "密歇根大学", "University of Michigan", "10:00"),
            new("EA.RATE", "EUR", "欧洲央行利率决议（存款便利利率）", "ECB Interest Rate Decision (Deposit Facility Rate)", "欧洲央行利率决议", "ECB Rate Decision",
                IndicatorCategory.InterestRate, IndicatorValueType.Value, IndicatorUnit.Percent, 2, ReleaseFrequency.Irregular, ImpactDirection.HigherIsBullish,
                "欧洲央行", "European Central Bank", "14:15"),
            new("EA.HICP.YOY.PRELIM", "EUR", "HICP 年率（初值）", "HICP YoY (Flash)", "HICP 年率初值", "HICP YoY Flash",
                IndicatorCategory.Inflation, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "欧盟统计局", "Eurostat", "11:00"),
            new("EA.COREHICP.YOY.PRELIM", "EUR", "核心 HICP 年率（初值）", "Core HICP YoY (Flash)", "核心 HICP 年率初值", "Core HICP YoY Flash",
                IndicatorCategory.Inflation, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "欧盟统计局", "Eurostat", "11:00"),
            new("EA.GDP.QOQ.PRELIM", "EUR", "GDP 季率（初值）", "GDP QoQ (Preliminary Flash)", "GDP 季率初值", "GDP QoQ Flash",
                IndicatorCategory.Growth, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Quarterly, ImpactDirection.HigherIsBullish,
                "欧盟统计局", "Eurostat", "11:00"),
            new("EA.UNEMP", "EUR", "失业率", "Unemployment Rate", "失业率", "Unemployment Rate",
                IndicatorCategory.Employment, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBearish,
                "欧盟统计局", "Eurostat", "11:00"),
            new("EA.PMI.MFG.PRELIM", "EUR", "HCOB 制造业 PMI（初值）", "HCOB Manufacturing PMI (Flash)", "制造业 PMI 初值", "Manufacturing PMI Flash",
                IndicatorCategory.Sentiment, IndicatorValueType.Value, IndicatorUnit.Index, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "标普全球", "S&P Global", "10:00"),
            new("EA.PMI.SVC.PRELIM", "EUR", "HCOB 服务业 PMI（初值）", "HCOB Services PMI (Flash)", "服务业 PMI 初值", "Services PMI Flash",
                IndicatorCategory.Sentiment, IndicatorValueType.Value, IndicatorUnit.Index, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "标普全球", "S&P Global", "10:00"),
            new("DE.CPI.MOM.PRELIM", "EUR", "CPI 月率（初值）", "CPI MoM (Preliminary)", "CPI 月率初值", "CPI MoM Prelim",
                IndicatorCategory.Inflation, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "德国联邦统计局", "Destatis", "14:00"),
            new("DE.IFO", "EUR", "ifo 商业景气指数", "ifo Business Climate Index", "ifo 商业景气", "ifo Business Climate",
                IndicatorCategory.Sentiment, IndicatorValueType.Value, IndicatorUnit.Index, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "ifo 经济研究所", "ifo Institute", "10:00"),
            new("DE.ZEW", "EUR", "ZEW 经济景气指数", "ZEW Economic Sentiment", "ZEW 经济景气", "ZEW Economic Sentiment",
                IndicatorCategory.Sentiment, IndicatorValueType.Value, IndicatorUnit.Index, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "欧洲经济研究中心", "ZEW", "11:00"),
            new("GB.RATE", "GBP", "英国央行利率决议", "BoE Interest Rate Decision", "英国央行利率决议", "BoE Rate Decision",
                IndicatorCategory.InterestRate, IndicatorValueType.Value, IndicatorUnit.Percent, 2, ReleaseFrequency.Irregular, ImpactDirection.HigherIsBullish,
                "英国央行", "Bank of England", "12:00"),
            new("GB.CPI.YOY", "GBP", "CPI 年率", "CPI YoY", "CPI 年率", "CPI YoY",
                IndicatorCategory.Inflation, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "英国国家统计局", "Office for National Statistics", "07:00"),
            new("GB.CORECPI.YOY", "GBP", "核心 CPI 年率", "Core CPI YoY", "核心 CPI 年率", "Core CPI YoY",
                IndicatorCategory.Inflation, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "英国国家统计局", "Office for National Statistics", "07:00"),
            new("GB.GDP.MOM", "GBP", "GDP 月率", "GDP MoM", "GDP 月率", "GDP MoM",
                IndicatorCategory.Growth, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "英国国家统计局", "Office for National Statistics", "07:00"),
            new("GB.GDP.QOQ", "GBP", "GDP 季率（首次公布）", "GDP QoQ (First Estimate)", "GDP 季率", "GDP QoQ",
                IndicatorCategory.Growth, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Quarterly, ImpactDirection.HigherIsBullish,
                "英国国家统计局", "Office for National Statistics", "07:00"),
            new("GB.UNEMP", "GBP", "失业率", "ILO Unemployment Rate", "失业率", "Unemployment Rate",
                IndicatorCategory.Employment, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBearish,
                "英国国家统计局", "Office for National Statistics", "07:00"),
            new("GB.AWE.3MYOY", "GBP", "平均每周工资（含奖金，3 个月年率）", "Average Earnings Including Bonus (3M YoY)", "平均每周工资", "Avg Earnings 3M YoY",
                IndicatorCategory.Employment, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "英国国家统计局", "Office for National Statistics", "07:00"),
            new("GB.RETAIL.MOM", "GBP", "零售销售月率", "Retail Sales MoM", "零售销售月率", "Retail Sales MoM",
                IndicatorCategory.Consumption, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "英国国家统计局", "Office for National Statistics", "07:00"),
            new("JP.RATE", "JPY", "日本央行利率决议", "BoJ Interest Rate Decision", "日本央行利率决议", "BoJ Rate Decision",
                IndicatorCategory.InterestRate, IndicatorValueType.Value, IndicatorUnit.Percent, 2, ReleaseFrequency.Irregular, ImpactDirection.HigherIsBullish,
                "日本央行", "Bank of Japan", "12:00"),
            new("JP.CORECPI.YOY", "JPY", "全国核心 CPI 年率", "National Core CPI YoY", "全国核心 CPI 年率", "National Core CPI YoY",
                IndicatorCategory.Inflation, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "日本总务省统计局", "Statistics Bureau of Japan", "08:30"),
            new("JP.TOKYO.CORECPI.YOY", "JPY", "东京核心 CPI 年率", "Tokyo Core CPI YoY", "东京核心 CPI 年率", "Tokyo Core CPI YoY",
                IndicatorCategory.Inflation, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "日本总务省统计局", "Statistics Bureau of Japan", "08:30"),
            new("JP.GDP.QOQ.PRELIM", "JPY", "GDP 季率（初值）", "GDP QoQ (Preliminary)", "GDP 季率初值", "GDP QoQ Prelim",
                IndicatorCategory.Growth, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Quarterly, ImpactDirection.HigherIsBullish,
                "日本内阁府", "Cabinet Office", "08:50"),
            new("JP.TANKAN.MFG", "JPY", "短观大型制造业景气判断指数", "Tankan Large Manufacturers Index", "短观大型制造业指数", "Tankan Large Mfg Index",
                IndicatorCategory.Sentiment, IndicatorValueType.Value, IndicatorUnit.Index, 0, ReleaseFrequency.Quarterly, ImpactDirection.HigherIsBullish,
                "日本央行", "Bank of Japan", "08:50"),
            new("CN.GDP.YOY", "CNY", "GDP 年率", "GDP YoY", "GDP 年率", "GDP YoY",
                IndicatorCategory.Growth, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Quarterly, ImpactDirection.HigherIsBullish,
                "国家统计局", "National Bureau of Statistics", "10:00"),
            new("CN.CPI.YOY", "CNY", "CPI 年率", "CPI YoY", "CPI 年率", "CPI YoY",
                IndicatorCategory.Inflation, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "国家统计局", "National Bureau of Statistics", "09:30"),
            new("CN.PPI.YOY", "CNY", "PPI 年率", "PPI YoY", "PPI 年率", "PPI YoY",
                IndicatorCategory.Inflation, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "国家统计局", "National Bureau of Statistics", "09:30"),
            new("CN.IP.YOY", "CNY", "规模以上工业增加值年率", "Industrial Production YoY", "工业增加值年率", "Industrial Production YoY",
                IndicatorCategory.Growth, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "国家统计局", "National Bureau of Statistics", "10:00"),
            new("CN.RETAIL.YOY", "CNY", "社会消费品零售总额年率", "Retail Sales YoY", "社零年率", "Retail Sales YoY",
                IndicatorCategory.Consumption, IndicatorValueType.Value, IndicatorUnit.Percent, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "国家统计局", "National Bureau of Statistics", "10:00"),
            new("CN.PMI.MFG", "CNY", "官方制造业 PMI", "NBS Manufacturing PMI", "官方制造业 PMI", "NBS Manufacturing PMI",
                IndicatorCategory.Sentiment, IndicatorValueType.Value, IndicatorUnit.Index, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "国家统计局", "National Bureau of Statistics", "09:30"),
            new("CN.TRADE", "CNY", "贸易帐（美元计）", "Trade Balance (USD)", "贸易帐", "Trade Balance",
                IndicatorCategory.Trade, IndicatorValueType.Value, IndicatorUnit.HundredMillionUsd, 1, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "海关总署", "General Administration of Customs", "11:00"),
            new("CN.LPR.1Y", "CNY", "1 年期贷款市场报价利率", "1-Year Loan Prime Rate", "1 年期 LPR", "1Y LPR",
                IndicatorCategory.InterestRate, IndicatorValueType.Value, IndicatorUnit.Percent, 2, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "全国银行间同业拆借中心", "National Interbank Funding Center", "09:00"),
            new("CN.LPR.5Y", "CNY", "5 年期以上贷款市场报价利率", "5-Year Loan Prime Rate", "5 年期以上 LPR", "5Y LPR",
                IndicatorCategory.InterestRate, IndicatorValueType.Value, IndicatorUnit.Percent, 2, ReleaseFrequency.Monthly, ImpactDirection.HigherIsBullish,
                "全国银行间同业拆借中心", "National Interbank Funding Center", "09:00"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                INSERT INTO ref_indicator (code, economy_code, currency_code, name_zh, name_en, short_name_zh, short_name_en,
                    category, value_type, unit, decimals, frequency, importance, direction, flat_tolerance,
                    agency_zh, agency_en, default_release_time, is_enabled, created_by, created_at)
                VALUES
                {string.Join("," + Environment.NewLine, Indicators.Select(Values))}
                ON CONFLICT (code) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 只删本迁移写入的行。指标被日历事件等引用之后不要用 Down 回退，按 PRD §8.4 停用后重新导入
            migrationBuilder.Sql($"""
                DELETE FROM ref_indicator
                WHERE created_by = {Text(CreatedBy)} AND code IN ({string.Join(", ", Indicators.Select(i => Text(i.Code)))});
                """);
        }

        private static string Values(Row i)
            => $"({Text(i.Code)}, {Text(i.Code[..2])}, {Text(i.CurrencyCode)}, {Text(i.NameZh)}, {Text(i.NameEn)}, {Text(i.ShortNameZh)}, {Text(i.ShortNameEn)}, "
               + $"{(short)i.Category}, {(short)i.ValueType}, {(short)i.Unit}, {i.Decimals}, {(short)i.Frequency}, {(short)Importance.High}, {(short)i.Direction}, 0, "
               + $"{Text(i.AgencyZh)}, {Text(i.AgencyEn)}, TIME {Text(i.DefaultReleaseTime)}, TRUE, {Text(CreatedBy)}, TIMESTAMPTZ {Text(CreatedAt)})";

        private static string Text(string value) => "'" + value.Replace("'", "''") + "'";

        /// <summary>经济体取编码的前两位</summary>
        private sealed record Row(
            string Code,
            string CurrencyCode,
            string NameZh,
            string NameEn,
            string ShortNameZh,
            string ShortNameEn,
            IndicatorCategory Category,
            IndicatorValueType ValueType,
            IndicatorUnit Unit,
            short Decimals,
            ReleaseFrequency Frequency,
            ImpactDirection Direction,
            string AgencyZh,
            string AgencyEn,
            string DefaultReleaseTime);
    }
}
