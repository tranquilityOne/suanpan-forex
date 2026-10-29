using Suanpan.Collector.Execution;
using Suanpan.Collector.Sources.US.Bls;
using Xunit;

namespace Suanpan.Collector.Tests.Sources.US.Bls;

/// <summary>BLS 任务参数的校验：写错时不发请求，运行记为失败并说明原因（日历设计 §5.1 外部标识的写法）</summary>
public class BlsParametersTests
{
    [Theory]
    [InlineData("""{"series": ["cuur0000sa0"]}""")]
    [InlineData("""{"series": ["CUUR0000SA0:yoy"]}""")]
    [InlineData("""{"releases": ["empsit"]}""")]
    [InlineData("""{"series": []}""")]
    public void Series_RejectsMalformedParameters(string parameters)
        => Assert.Throws<FetchParametersException>(() => BlsParameters.Series(parameters));

    [Fact]
    public void Releases_RejectsKeysThatAreNotFileNames()
        => Assert.Throws<FetchParametersException>(() => BlsParameters.Releases("""{"releases": ["../empsit"]}"""));
}
