using Suanpan.Core.Common.Localization;
using Suanpan.Core.Reference.Enums;

namespace Suanpan.Services.Reference.IndicatorSources.Models;

/// <summary>映射列表的一行；数据源停用时页面标出（ERR-IND-003）</summary>
public sealed record IndicatorSourceRow(
    long Id,
    string IndicatorCode,
    BilingualText IndicatorShortName,
    long SourceId,
    string SourceName,
    bool SourceEnabled,
    IndicatorSourcePurpose Purpose,
    string ExternalId,
    string ParserKey,
    short? Priority)
{
    /// <summary>解析器的中文名；解析器已不在目录中时为空</summary>
    public string? ParserName { get; init; }
}
