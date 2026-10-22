using Suanpan.Core.Common.Auditing;
using Suanpan.Core.Common.Localization;

namespace Suanpan.Core.Reference.Entities;

/// <summary>经济体（ref_economy，基础数据设计 §2.1）</summary>
public class Economy : AuditedEntity
{
    /// <summary>ISO 3166-1 alpha-2 编码，欧元区为 EA；主键，创建后不可修改（BR-026）</summary>
    public string Code { get; set; } = string.Empty;

    public BilingualText Name { get; set; } = null!;

    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>IANA 时区，如 America/New_York（BR-027）；本经济体指标的默认发布时间按这个时区解释</summary>
    public string TimeZone { get; set; } = string.Empty;

    /// <summary>列表和下拉框中的显示顺序</summary>
    public short Sort { get; set; }

    public bool IsEnabled { get; set; } = true;
}
