using System.Globalization;

namespace Suanpan.Core.Reference.Rules;

/// <summary>
/// 经济体的时区只能是 IANA 名称（基础数据设计 §3.2，BR-027、ERR-REF-003），如 America/New_York；
/// Windows 名称（如 Eastern Standard Time）不算。.NET 在 Windows 和 Linux 上都能识别 IANA 名称。
/// </summary>
public static class TimeZoneRule
{
    /// <summary>下拉框中出现的大区：排除 US/Eastern、Etc/GMT+5 这类旧别名</summary>
    private static readonly HashSet<string> Regions = new(StringComparer.Ordinal)
    {
        "Africa", "America", "Antarctica", "Asia", "Atlantic", "Australia", "Europe", "Indian", "Pacific",
    };

    private static readonly Lazy<IReadOnlyList<string>> AllIanaIds = new(LoadIanaIds);

    public static bool IsValid(string? ianaId)
        => !string.IsNullOrWhiteSpace(ianaId)
           && TimeZoneInfo.TryFindSystemTimeZoneById(ianaId, out var zone)
           && zone.HasIanaId;

    /// <summary>
    /// 下拉框的选项：IANA 名称，按字母排序。Linux 上直接取系统时区；Windows 上系统时区是 Windows 名称，
    /// 按各国家和地区转换成 IANA 名称（如 Romance Standard Time 在比利时为 Europe/Brussels），避免只剩每个 Windows 时区的一个代表城市。
    /// </summary>
    public static IReadOnlyList<string> IanaIds => AllIanaIds.Value;

    private static IReadOnlyList<string> LoadIanaIds()
    {
        var regionCodes = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Select(c => c.Name.Split('-').LastOrDefault())
            .Where(r => r is { Length: 2 })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var zone in TimeZoneInfo.GetSystemTimeZones())
        {
            if (zone.HasIanaId)
            {
                ids.Add(zone.Id);
                continue;
            }

            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var defaultId))
            {
                ids.Add(defaultId);
            }

            foreach (var region in regionCodes)
            {
                if (TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, region, out var regionalId))
                {
                    ids.Add(regionalId);
                }
            }
        }

        return ids
            .Where(id => Regions.Contains(id.Split('/')[0]) && IsValid(id))
            .Order(StringComparer.Ordinal)
            .ToList();
    }
}
