using Suanpan.Core.Common.Localization;
using Suanpan.Core.Reference.Enums;

namespace Suanpan.Services.Reference.Indicators.Models;

/// <summary>指标列表的一行</summary>
public sealed record IndicatorRow(
    string Code,
    BilingualText ShortName,
    string EconomyCode,
    BilingualText EconomyName,
    Importance Importance,
    ImpactDirection Direction,
    ReleaseFrequency Frequency,
    bool IsEnabled);
