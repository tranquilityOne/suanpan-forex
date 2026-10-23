namespace Suanpan.Infrastructure.Http;

/// <summary>采集用的命名 HttpClient（采集设计 §5.4）：<c>httpClientFactory.CreateClient(CollectHttp.ClientName)</c></summary>
public static class CollectHttp
{
    public const string ClientName = "collect";
}
