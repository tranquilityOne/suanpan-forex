using Suanpan.Core.Sys.Rules;
using Suanpan.Services.Sys.Configs;
using Xunit;

namespace Suanpan.Services.Tests.Sys.Configs;

public class ConfigDefinitionsTests
{
    /// <summary>新增配置项时容易写错默认值的格式，或者在不同模块中重复使用同一个键</summary>
    [Fact]
    public void Definitions_HaveUniqueKeysAndDefaultsMatchingTheirTypes()
    {
        Assert.Equal(ConfigDefinitions.All.Count, ConfigDefinitions.All.Select(d => d.Key).Distinct().Count());
        Assert.All(ConfigDefinitions.All, d =>
        {
            Assert.True(ConfigValueRule.TryNormalize(d.ValueType, d.DefaultValue, out var normalized), d.Key);
            Assert.Equal(normalized, d.DefaultValue);
        });
    }
}
