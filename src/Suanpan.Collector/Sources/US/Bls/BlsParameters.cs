using System.Text.Json;
using System.Text.RegularExpressions;
using Suanpan.Collector.Execution;

namespace Suanpan.Collector.Sources.US.Bls;

/// <summary>
/// BLS 采集任务的参数（JSON 对象）：发布日程任务为 <c>{"releases": ["empsit", "cpi"]}</c>，
/// 公布值任务为 <c>{"series": ["CES0000000001:chg", "LNS14000000"]}</c>。适配器和解析器共用，参数不对时抛出 <see cref="FetchParametersException"/>
/// </summary>
public static partial class BlsParameters
{
    /// <summary>发布标识：日程页的文件名，如 empsit</summary>
    public static IReadOnlyList<string> Releases(string? parameters)
    {
        var releases = Strings(parameters, "releases");
        return releases.FirstOrDefault(r => !ReleaseKeyFormat().IsMatch(r)) is { } invalid
            ? throw new FetchParametersException($"发布标识 \"{invalid}\" 应为小写字母和数字，如 empsit")
            : releases;
    }

    /// <summary>要取的外部标识（与公布值映射一致）</summary>
    public static IReadOnlyList<BlsSeriesSpec> Series(string? parameters)
        => Strings(parameters, "series").Select(BlsSeriesSpec.Parse).ToList();

    /// <summary>数据源的基础地址，补上末尾的 /，相对地址接在它后面</summary>
    public static Uri BaseUri(string baseUrl) => new(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");

    private static IReadOnlyList<string> Strings(string? parameters, string name)
    {
        JsonElement array;
        try
        {
            using var document = JsonDocument.Parse(parameters ?? "{}");
            array = document.RootElement.TryGetProperty(name, out var value) ? value.Clone() : default;
        }
        catch (JsonException)
        {
            throw new FetchParametersException("不是合法的 JSON");
        }

        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() == 0)
        {
            throw new FetchParametersException($"缺少 {name}（非空的字符串数组）");
        }

        var values = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                throw new FetchParametersException($"{name} 中只能是非空的字符串");
            }

            values.Add(item.GetString()!.Trim());
        }

        return values.Distinct(StringComparer.Ordinal).ToList();
    }

    [GeneratedRegex(@"^[a-z0-9]+\z")]
    private static partial Regex ReleaseKeyFormat();
}
