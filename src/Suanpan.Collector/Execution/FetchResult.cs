using System.Net.Http.Headers;
using System.Text;

namespace Suanpan.Collector.Execution;

/// <summary>适配器取回的一个或多个响应（如分页），供解析和以后的快照（FR-004）使用</summary>
public sealed record FetchResult(IReadOnlyList<FetchedResponse> Responses);

/// <summary>一个响应：地址、状态码、内容类型和原始内容</summary>
public sealed record FetchedResponse(Uri Url, int StatusCode, string? ContentType, byte[] Body)
{
    public bool IsSuccess => StatusCode is >= 200 and < 300;

    /// <summary>按内容类型声明的字符集解码，没有声明或不认识时按 UTF-8</summary>
    public string Text()
    {
        var encoding = Encoding.UTF8;
        if (ContentType is not null && MediaTypeHeaderValue.TryParse(ContentType, out var mediaType) && mediaType.CharSet is { Length: > 0 } charset)
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

        return encoding.GetString(Body);
    }
}
