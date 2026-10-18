using System.Net;

namespace Suanpan.Crm.Tests.Fixtures;

/// <summary>构造测试请求、读取跳转目标</summary>
public static class Requests
{
    public static HttpRequestMessage Get(string path, string? cookie = null) => Create(HttpMethod.Get, path, cookie);

    public static HttpRequestMessage Post(string path, string? cookie = null, IReadOnlyDictionary<string, string>? form = null)
    {
        var request = Create(HttpMethod.Post, path, cookie);
        request.Content = new FormUrlEncodedContent(form ?? new Dictionary<string, string>());
        return request;
    }

    /// <summary>标记为 HTMX 请求</summary>
    public static HttpRequestMessage AsHtmx(this HttpRequestMessage request)
    {
        request.Headers.Add("HX-Request", "true");
        return request;
    }

    /// <summary>模拟请求来源：remoteIp 是直接连到 Crm 的地址，forwardedFor 是代理传来的 X-Forwarded-For</summary>
    public static HttpRequestMessage From(this HttpRequestMessage request, string remoteIp, string? forwardedFor = null)
    {
        request.Headers.Add(CrmApp.RemoteIpHeader, remoteIp);
        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        return request;
    }

    /// <summary>302 跳转的目标路径（不含查询串）；不是 302 时返回 null。Cookie 认证给出绝对地址，MVC 的跳转给出相对地址</summary>
    public static string? RedirectPath(this HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.Redirect || response.Headers.Location is not { } location)
        {
            return null;
        }

        return location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString.Split('?')[0];
    }

    private static HttpRequestMessage Create(HttpMethod method, string path, string? cookie)
    {
        var request = new HttpRequestMessage(method, path);
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        return request;
    }
}
