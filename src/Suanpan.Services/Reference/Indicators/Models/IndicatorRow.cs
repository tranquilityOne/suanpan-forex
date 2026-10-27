using Suanpan.Core.Common.Localization;
using Suanpan.Core.Reference.Enums;

namespace Suanpan.Services.Reference.Indicators.Models;

/// <summary>指标列表的一行；SourceCount 为映射的数据源数，大于 0 时不能删除（BR-153）</summary>
public sealed record IndicatorRow(
    string Code,
    BilingualText ShortName,
    string EconomyCode,
    BilingualText EconomyName,
    Importance Importance,
    ImpactDirection Direction,
    ReleaseFrequency Frequency,
    bool IsEnabled,
    int SourceCount);
