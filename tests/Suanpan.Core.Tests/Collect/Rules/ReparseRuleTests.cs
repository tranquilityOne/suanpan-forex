using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Parsers;
using Suanpan.Core.Collect.Rules;
using Xunit;

namespace Suanpan.Core.Tests.Collect.Rules;

/// <summary>重新解析的规则（采集设计 §5.6，FR-004 主流程第 3 步、BR-024）</summary>
public class ReparseRuleTests
{
    private static readonly ParserDescriptor TaskParser = new("us.test.schedule", "日程", CollectTargetType.Calendar);

    /// <summary>可以改选同一适配器、目标数据类型相同的解析器；其他适配器、其他目标数据类型、目录中没有的都拒绝</summary>
    [Theory]
    [InlineData("us.test.schedule_v2", CollectTargetType.Calendar, true)]
    [InlineData("us.other.schedule", CollectTargetType.Calendar, false)]
    [InlineData("us.test.release", CollectTargetType.Release, false)]
    public void ValidateParser_SameAdapterAndTargetType(string key, CollectTargetType targetType, bool allowed)
        => Assert.Equal(allowed, ReparseRule.ValidateParser(TaskParser, new ParserDescriptor(key, "解析器", targetType)) is null);

    [Fact]
    public void ValidateParser_RejectsParserMissingFromCatalog()
        => Assert.NotNull(ReparseRule.ValidateParser(TaskParser, chosen: null));

    /// <summary>BR-024、ERR-PARSE-004：最多 5000 个；没有快照也不能发起</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5000, true)]
    [InlineData(5001, false)]
    public void ValidateCount_UpTo5000(int count, bool allowed)
        => Assert.Equal(allowed, ReparseRule.ValidateCount(count) is null);
}
