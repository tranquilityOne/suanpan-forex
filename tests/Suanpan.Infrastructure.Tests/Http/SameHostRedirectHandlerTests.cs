using System.Net;
using Suanpan.Infrastructure.Http;
using Xunit;

namespace Suanpan.Infrastructure.Tests.Http;

/// <summary>采集请求只跟随同一主机内的跳转（采集设计 §5.4）</summary>
public class SameHostRedirectHandlerTests
{
    [Fact]
    public async Task FollowsRedirectWithinTheSameHost()
    {
        var server = new StubServer
        {
            ["http://data.example.org/a"] = Redirect("https://data.example.org/b"),
            ["https://data.example.org/b"] = Redirect("/c"),
            ["https://data.example.org/c"] = new HttpResponseMessage(HttpStatusCode.OK),
        };

        using var response = await Send(server, "http://data.example.org/a");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["http://data.example.org/a", "https://data.example.org/b", "https://data.example.org/c"], server.Requested);
    }

    [Fact]
    public async Task ReturnsRedirectToAnotherHostWithoutFollowing()
    {
        var server = new StubServer { ["https://data.example.org/a"] = Redirect("https://elsewhere.example.com/a") };

        using var response = await Send(server, "https://data.example.org/a");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(["https://data.example.org/a"], server.Requested);
    }

    [Fact]
    public async Task StopsAfterMaxRedirects()
    {
        var server = new StubServer();
        for (var i = 0; i <= SameHostRedirectHandler.MaxRedirects + 1; i++)
        {
            server[$"https://data.example.org/{i}"] = Redirect($"/{i + 1}");
        }

        using var response = await Send(server, "https://data.example.org/0");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(SameHostRedirectHandler.MaxRedirects + 1, server.Requested.Count);
    }

    private static async Task<HttpResponseMessage> Send(StubServer server, string url)
    {
        using var client = new HttpClient(new SameHostRedirectHandler { InnerHandler = server });
        return await client.GetAsync(url, TestContext.Current.CancellationToken);
    }

    private static HttpResponseMessage Redirect(string location)
        => new(HttpStatusCode.Found) { Headers = { Location = new Uri(location, UriKind.RelativeOrAbsolute) } };

    /// <summary>按完整地址返回预设的响应，记下请求过的地址</summary>
    private sealed class StubServer : HttpMessageHandler
    {
        private readonly Dictionary<string, HttpResponseMessage> _responses = [];

        public List<string> Requested { get; } = [];

        public HttpResponseMessage this[string url]
        {
            set => _responses[url] = value;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            Requested.Add(url);
            return Task.FromResult(_responses.TryGetValue(url, out var response) ? response : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
