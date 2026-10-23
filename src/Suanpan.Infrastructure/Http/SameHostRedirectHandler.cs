using System.Net;

namespace Suanpan.Infrastructure.Http;

/// <summary>
/// 采集请求只跟随同一主机内的跳转（采集设计 §5.4）：如 http 改 https、路径改名。跳到别的域名时不跟随，
/// 把跳转响应原样返回，由调用方当作非 2xx 处理，避免数据源把请求引到未登记、未做合规确认的地址。
/// 只用于 GET 请求；最多跟随 <see cref="MaxRedirects"/> 次。
/// </summary>
public sealed class SameHostRedirectHandler : DelegatingHandler
{
    public const int MaxRedirects = 5;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        for (var redirects = 0; redirects < MaxRedirects && IsRedirect(response.StatusCode); redirects++)
        {
            var current = request.RequestUri!;
            if (response.Headers.Location is not { } location)
            {
                return response;
            }

            var target = location.IsAbsoluteUri ? location : new Uri(current, location);
            if (!string.Equals(target.Host, current.Host, StringComparison.OrdinalIgnoreCase)
                || (target.Scheme != Uri.UriSchemeHttp && target.Scheme != Uri.UriSchemeHttps))
            {
                return response;
            }

            response.Dispose();
            request = CopyAsGet(request, target);
            response = await base.SendAsync(request, cancellationToken);
        }

        return response;
    }

    private static bool IsRedirect(HttpStatusCode status) => status is HttpStatusCode.MovedPermanently or HttpStatusCode.Found
        or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static HttpRequestMessage CopyAsGet(HttpRequestMessage original, Uri target)
    {
        var copy = new HttpRequestMessage(HttpMethod.Get, target) { Version = original.Version, VersionPolicy = original.VersionPolicy };
        foreach (var header in original.Headers)
        {
            copy.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return copy;
    }
}
