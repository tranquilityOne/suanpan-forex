using Suanpan.Core.Common.Auditing;
using Suanpan.Core.Common.Localization;
using Suanpan.Core.Reference.Enums;

namespace Suanpan.Core.Reference.Entities;

/// <summary>指标（ref_indicator，基础数据设计 §2.1）；保存时的校验见 IndicatorRule</summary>
public class Indicator : AuditedEntity
{
    /// <summary>主键，如 US.NFP、US.CPI.YOY（BR-029）；创建后不可修改</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>所属经济体；创建后不可修改：编码以经济体开头</summary>
    public string EconomyCode { get; set; } = string.Empty;

    /// <summary>新增时默认取经济体的货币，可以改（如德国指标对应 EUR）</summary>
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>全称</summary>
    public BilingualText Name { get; set; } = null!;

    /// <summary>简称，日历和快讯中使用</summary>
    public BilingualText ShortName { get; set; } = null!;

    public IndicatorCategory Category { get; set; }

    public IndicatorValueType ValueType { get; set; }

    public IndicatorUnit Unit { get; set; }

    /// <summary>小数位，0 ~ 6（数值列为 numeric(18,6)）</summary>
    public short Decimals { get; set; }

    public ReleaseFrequency Frequency { get; set; }

    public Importance Importance { get; set; }

    public ImpactDirection Direction { get; set; }

    /// <summary>持平容差：公布值与预期之差的绝对值不超过它时算持平（BR-074）</summary>
    public decimal FlatTolerance { get; set; }

    /// <summary>合理下限（BR-133，质量校验 FR-026 使用）</summary>
    public decimal? RangeMin { get; set; }

    /// <summary>合理上限（BR-133，质量校验 FR-026 使用）</summary>
    public decimal? RangeMax { get; set; }

    /// <summary>偏离几倍标准差算异常；为空时用 IndicatorRule.DefaultSigmaN（BR-133，FR-026 使用）</summary>
    public decimal? SigmaN { get; set; }

    /// <summary>发布机构，如 美国劳工统计局 / Bureau of Labor Statistics</summary>
    public BilingualText Agency { get; set; } = null!;

    /// <summary>默认发布时间，机构当地时间，时区取经济体的 TimeZone</summary>
    public TimeOnly DefaultReleaseTime { get; set; }

    /// <summary>说明（定义、计算方式、为什么重要），前台指标详情页使用；为空时前台不展示（BR-033）</summary>
    public BilingualText? Description { get; set; }

    public bool IsEnabled { get; set; } = true;
}
