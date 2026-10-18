using Suanpan.Core.Sys.Rules;
using Suanpan.Services.Sys.Configs;
using Xunit;

namespace Suanpan.Services.Tests.Sys.Configs;

public class SysConfigKeysTests
{
    /// <summary>新增配置项时容易写错默认值的格式，或者重复使用同一个键</summary>
    [Fact]
    public void Definitions_HaveUniqueKeysAndDefaultsMatchingTheirTypes()
    {
        Assert.Equal(SysConfigKeys.Definitions.Count, SysConfigKeys.Definitions.Select(d => d.Key).Distinct().Count());
        Assert.All(SysConfigKeys.Definitions, d =>
        {
            Assert.True(ConfigValueRule.TryNormalize(d.ValueType, d.DefaultValue, out var normalized), d.Key);
            Assert.Equal(normalized, d.DefaultValue);
        });
    }
}
