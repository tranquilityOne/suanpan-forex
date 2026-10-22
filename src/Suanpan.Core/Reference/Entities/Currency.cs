using Suanpan.Core.Common.Auditing;
using Suanpan.Core.Common.Localization;

namespace Suanpan.Core.Reference.Entities;

/// <summary>货币（ref_currency，基础数据设计 §2.1）</summary>
public class Currency : AuditedEntity
{
    /// <summary>ISO 4217 编码，如 USD；主键，创建后不可修改（BR-026）</summary>
    public string Code { get; set; } = string.Empty;

    public BilingualText Name { get; set; } = null!;

    /// <summary>停用后不能再被新选用，已有的引用不受影响（BR-028）</summary>
    public bool IsEnabled { get; set; } = true;
}
