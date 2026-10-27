using Microsoft.Extensions.DependencyInjection;
using Suanpan.Collector.Execution;
using Suanpan.Core.Collect.Parsers;
using Xunit;

namespace Suanpan.Collector.Tests.Execution;

public class CollectorRegistrationTests
{
    /// <summary>
    /// 采集设计 §5.2：解析器目录（Crm 的下拉框和校验用它）与 Collector 注册的解析器一一对应，每个解析器都有所属的适配器。
    /// 新增解析器时只登记了一边，任务能配置却执行不了，或者能执行却选不到
    /// </summary>
    [Fact]
    public void ParserCatalog_MatchesRegisteredImplementations()
    {
        using var provider = new ServiceCollection().AddSuanpanCollectors().BuildServiceProvider();
        var parsers = provider.GetServices<IParser>().Select(p => p.Key).Order(StringComparer.Ordinal).ToList();
        var adapters = provider.GetServices<ICollectAdapter>().Select(a => a.Key).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(ParserCatalog.All.Select(p => p.Key).Order(StringComparer.Ordinal), parsers);
        Assert.All(ParserCatalog.All, parser => Assert.Contains(parser.AdapterKey, adapters));
    }
}
