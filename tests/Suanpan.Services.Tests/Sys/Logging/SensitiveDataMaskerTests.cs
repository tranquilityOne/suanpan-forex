using Suanpan.Services.Sys.Logging;
using Xunit;

namespace Suanpan.Services.Tests.Sys.Logging;

public class SensitiveDataMaskerTests
{
    public static TheoryData<string> AllSensitiveWords => new(SensitiveDataMasker.SensitiveWords);

    /// <summary>完成标准：覆盖全部敏感词</summary>
    [Theory]
    [MemberData(nameof(AllSensitiveWords))]
    public void EveryWord_MasksTopLevelArgumentContainingIt(string word)
    {
        var json = SensitiveDataMasker.SerializeArguments(new Dictionary<string, object?> { [$"new_{word.ToUpperInvariant()}_value"] = "plain-secret" });

        Assert.NotNull(json);
        Assert.DoesNotContain("plain-secret", json);
        Assert.Contains(SensitiveDataMasker.Masked, json);
    }

    [Fact]
    public void NestedObjectsAndArrays_AreMasked()
    {
        var input = new { Username = "zhangsan", Password = "plain-secret", Items = new[] { new { ApiKey = "plain-secret" } } };

        var json = SensitiveDataMasker.SerializeArguments(new Dictionary<string, object?> { ["input"] = input });

        Assert.NotNull(json);
        Assert.Contains("zhangsan", json);
        Assert.DoesNotContain("plain-secret", json);
    }

    [Theory]
    [InlineData("config_key")]
    [InlineData("perm_code")]
    [InlineData("ConfigKey")]
    public void NormalFields_AreNotMasked(string name)
    {
        var json = SensitiveDataMasker.SerializeArguments(new Dictionary<string, object?> { [name] = "visible-value" });

        Assert.NotNull(json);
        Assert.Contains("visible-value", json);
    }

    [Fact]
    public void CancellationTokenOnly_ProducesNoParams()
    {
        Assert.Null(SensitiveDataMasker.SerializeArguments(new Dictionary<string, object?> { ["ct"] = CancellationToken.None }));
    }

    [Fact]
    public void LongParams_AreTruncated()
    {
        var json = SensitiveDataMasker.SerializeArguments(new Dictionary<string, object?> { ["text"] = new string('a', 5000) });

        Assert.Equal(SensitiveDataMasker.MaxLength, json!.Length);
    }
}
