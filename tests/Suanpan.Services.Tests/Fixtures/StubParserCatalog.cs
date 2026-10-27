using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Parsers;
using Suanpan.Services.Collect.Tasks;

namespace Suanpan.Services.Tests.Fixtures;

/// <summary>解析器目录的替身：M1 的目录为空，测试中提供两个解析器</summary>
internal sealed class StubParserCatalog : IParserCatalog
{
    public static readonly ParserDescriptor Schedule = new("us.test.schedule", "测试日程", CollectTargetType.Calendar);

    public static readonly ParserDescriptor News = new("us.test.news", "测试新闻", CollectTargetType.News);

    public IReadOnlyList<ParserDescriptor> All { get; } = [Schedule, News];

    public ParserDescriptor? Find(string? key) => All.FirstOrDefault(p => p.Key == key);
}
