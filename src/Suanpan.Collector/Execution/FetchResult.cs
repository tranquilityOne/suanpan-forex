using System.Net.Http.Headers;
using System.Text;

namespace Suanpan.Collector.Execution;

/// <summary>适配器取回的一个或多个响应（如分页），供解析和以后的快照（FR-004）使用</summary>
public sealed record FetchResult(IReadOnlyList<FetchedResponse> Responses);

/// <summary>一个响应：地址、状态码、内容类型和原始内容</summary>
public sealed record FetchedResponse(Uri Url, int StatusCode, string? ContentType, byte[] Body)
{
    /// <summary>
    /// 请求标识，判断快照"未变化"用（采集设计 §5.5，BR-021）：为空时取地址去掉查询串。
    /// 同一地址按查询串或请求体区分不同请求的数据源（如 BLS API 用 POST 按序列取数）由适配器给出，不能含凭据
    /// </summary>
    public string? RequestKey { get; init; }

    /// <summary>收到响应的时间（UTC），即快照的请求时间；经 FetchContext 发出的请求都有，为空时取保存快照的时间</summary>
    public DateTime? ReceivedAt { get; init; }

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
