using System.Text.Json;
using Suanpan.Collector.Execution;
using Suanpan.Core.Collect.Enums;
using Suanpan.Services.Collect.Ingest;

namespace Suanpan.Collector.Tests.Fixtures;

// 测试用的适配器、解析器和入库服务（采集设计 §11 第 6 项）：M1 没有真实的解析器，用它们验证执行框架

/// <summary>按任务参数 {"pages": n} 请求 {基础地址}page/1 ~ page/n，默认 1 页</summary>
public sealed class FakeAdapter : ICollectAdapter
{
    public const string AdapterKey = "zz.test";

    public string Key => AdapterKey;

    public async Task<FetchResult> FetchAsync(FetchContext context, CancellationToken ct)
    {
        var pages = context.Parameters is { } json && JsonDocument.Parse(json).RootElement.TryGetProperty("pages", out var value) ? value.GetInt32() : 1;
        var responses = new List<FetchedResponse>();
        for (var page = 1; page <= pages; page++)
        {
            responses.Add(await context.GetAsync(new Uri(new Uri(context.Source.BaseUrl), $"page/{page}"), ct));
        }

        return new FetchResult(responses);
    }
}

/// <summary>每个响应解析成一条记录（响应的文字）</summary>
public sealed class FakeParser : IParser
{
    public const string ParserKey = FakeAdapter.AdapterKey + ".items";

    public string Key => ParserKey;

    public ParseResult Parse(FetchResult fetched, string? parameters)
        => new(fetched.Responses.Select(r => (object)r.Text()).ToList(), []);
}

/// <summary>日历排期的假入库：每条记录算新增</summary>
public sealed class FakeIngestor : ICollectIngestor
{
    public CollectTargetType TargetType => CollectTargetType.Calendar;

    public Task<IngestResult> IngestAsync(IngestRequest request, CancellationToken ct)
        => Task.FromResult(new IngestResult(request.Records.Count, 0, 0, 0));
}
