namespace Suanpan.Crm.Mvc;

public static class HtmxRequestExtensions
{
    /// <summary>是否 HTMX 发起的请求（请求头 HX-Request: true，前端约定 §4.2）</summary>
    public static bool IsHtmx(this HttpRequest request)
        => string.Equals(request.Headers["HX-Request"], "true", StringComparison.OrdinalIgnoreCase);
}
