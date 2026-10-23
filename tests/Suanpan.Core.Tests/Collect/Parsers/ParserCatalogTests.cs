using System.Text.RegularExpressions;
using Suanpan.Core.Collect.Parsers;
using Xunit;

namespace Suanpan.Core.Tests.Collect.Parsers;

/// <summary>
/// 解析器目录的写法（采集设计 §5.2）：标识为 {经济体}.{机构}.{内容}，全部小写，不重复。
/// M1 目录为空，ENG-009 起加入解析器后由这里把关
/// </summary>
public partial class ParserCatalogTests
{
    [Fact]
    public void Keys_AreUniqueAndWellFormed()
    {
        var keys = ParserCatalog.All.Select(p => p.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(keys, key => Assert.Matches(KeyFormat(), key));
    }

    [GeneratedRegex(@"^[a-z]{2}\.[a-z0-9]+\.[a-z0-9_]+\z")]
    private static partial Regex KeyFormat();
}
