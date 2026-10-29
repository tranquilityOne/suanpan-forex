using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Suanpan.Crm.Models.Collect;

/// <summary>
/// 快照内容的显示和下载（采集设计 §5.5，BR-025）：文本类型（text/*、JSON、XML、CSV，或声明了字符集的）按字符集解码，只显示前 1 MB；
/// 其他类型只显示类型和大小。页面一律在 &lt;pre&gt; 中转义输出，不渲染、不执行
/// </summary>
public static class SnapshotContentView
{
    public const int MaxDisplayBytes = 1024 * 1024;

    /// <summary>中文不转成 \u 转义，便于阅读；页面输出时仍由 Razor 做 HTML 转义</summary>
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>文本类型返回解码后的内容和是否截断；其他类型返回 null</summary>
    public static (string Text, bool Truncated)? Text(string? contentType, byte[] content)
    {
        if (!MediaTypeHeaderValue.TryParse(contentType, out var mediaType) || !IsText(mediaType))
        {
            return null;
        }

        var encoding = Encoding.UTF8;
        if (mediaType.CharSet is { Length: > 0 } charset)
        {
            try
            {
                encoding = Encoding.GetEncoding(charset.Trim('"'));
            }
            catch (ArgumentException)
            {
                // 不认识的字符集按 UTF-8 解码
            }
        }

        var truncated = content.Length > MaxDisplayBytes;
        return (encoding.GetString(content, 0, truncated ? MaxDisplayBytes : content.Length), truncated);
    }

    /// <summary>内容大小：字节、KB、MB</summary>
    public static string Size(int bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / (1024.0 * 1024):0.#} MB",
    };

    /// <summary>下载的文件名：snapshot-{id} 加按内容类型推断的扩展名</summary>
    public static string FileName(long id, string? contentType)
    {
        var mediaType = MediaTypeHeaderValue.TryParse(contentType, out var parsed) ? parsed.MediaType?.ToLowerInvariant() : null;
        var extension = mediaType switch
        {
            "text/html" => ".html",
            "application/json" => ".json",
            "application/xml" or "text/xml" => ".xml",
            "text/csv" => ".csv",
            not null when mediaType.StartsWith("text/", StringComparison.Ordinal) => ".txt",
            _ => ".bin",
        };
        return $"snapshot-{id}{extension}";
    }

    /// <summary>运行记录的 parse_output 缩进后显示；格式不对时原样返回</summary>
    public static string ParseOutput(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, Indented);
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static bool IsText(MediaTypeHeaderValue mediaType)
    {
        var type = mediaType.MediaType?.ToLowerInvariant() ?? string.Empty;
        return type.StartsWith("text/", StringComparison.Ordinal)
            || type is "application/json" or "application/xml" or "application/csv"
            || type.EndsWith("+json", StringComparison.Ordinal) || type.EndsWith("+xml", StringComparison.Ordinal)
            || !string.IsNullOrEmpty(mediaType.CharSet);
    }
}
