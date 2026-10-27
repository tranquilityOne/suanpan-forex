using System.Collections.Concurrent;
using System.Net;

namespace Suanpan.Collector.Tests.Fixtures;

/// <summary>
/// 进程内的假 HTTP 服务：替换采集用 HttpClient 的最底层处理器，User-Agent、同主机跳转等上层处理器照常经过。
/// 每个测试用 <see cref="NewHost"/> 取一个随机主机名作为数据源的基础地址，按主机名设置应答、查看收到的请求，测试之间互不影响
/// </summary>
public sealed class StubHttp
{
    private readonly ConcurrentDictionary<string, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _routes = new();

    private readonly ConcurrentDictionary<string, ConcurrentQueue<StubRequest>> _requests = new();

    /// <summary>新的主机名；不指定应答时一律返回 200 "ok"</summary>
    public string NewHost(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? respond = null)
    {
        var host = $"t{Guid.NewGuid():N}.example.test";
        _routes[host] = respond ?? ((_, _) => Task.FromResult(Reply(HttpStatusCode.OK)));
        return host;
    }

    /// <summary>这个主机收到的请求，按收到的顺序</summary>
    public IReadOnlyList<StubRequest> Requests(string host) => _requests.TryGetValue(host, out var queue) ? queue.ToList() : [];

    public static HttpResponseMessage Reply(HttpStatusCode status, string body = "ok") => new(status) { Content = new StringContent(body) };

    /// <summary>每次创建新的处理器实例：HttpClientFactory 到期回收处理器时会释放它，共享的路由表不受影响</summary>
    public HttpMessageHandler CreateHandler() => new Handler(this);

    private Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var host = request.RequestUri!.Host;
        _requests.GetOrAdd(host, _ => new ConcurrentQueue<StubRequest>()).Enqueue(new StubRequest(request.RequestUri, request.Headers.UserAgent.ToString()));
        return _routes.TryGetValue(host, out var respond) ? respond(request, ct) : Task.FromResult(Reply(HttpStatusCode.NotFound));
    }

    private sealed class Handler(StubHttp stub) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => stub.SendAsync(request, cancellationToken);
    }
}

public sealed record StubRequest(Uri Url, string UserAgent);
