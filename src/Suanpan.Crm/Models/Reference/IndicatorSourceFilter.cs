using Suanpan.Core.Reference.Enums;
using Suanpan.Services.Reference.IndicatorSources.Models;

namespace Suanpan.Crm.Models.Reference;

/// <summary>指标数据源映射列表的筛选条件（基础数据设计 §6.2）</summary>
public sealed class IndicatorSourceFilter
{
    public string? IndicatorCode { get; set; }

    public long? SourceId { get; set; }

    public IndicatorSourcePurpose? Purpose { get; set; }

    public IndicatorSourceQuery ToQuery(int page = 1) => new(IndicatorCode, SourceId, Purpose, page);
}
