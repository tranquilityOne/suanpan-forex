using Suanpan.Core.Common.Localization;
using Suanpan.Core.Reference.Entities;
using Suanpan.Core.Reference.Enums;
using Suanpan.Core.Reference.Rules;
using Xunit;

namespace Suanpan.Core.Tests.Reference.Rules;

public class IndicatorRuleTests
{
    [Fact]
    public void Validate_AcceptsCompleteIndicator()
        => Assert.Null(IndicatorRule.Validate(Valid()));

    /// <summary>可空的字段：说明（BR-033）、合理上下限、标准差倍数；小数位、持平容差取边界值</summary>
    [Fact]
    public void Validate_AcceptsOptionalFieldsEmptyAndBoundaryValues()
    {
        var indicator = Valid();
        indicator.Description = null;
        indicator.RangeMin = null;
        indicator.RangeMax = null;
        indicator.SigmaN = null;
        indicator.Decimals = IndicatorRule.MaxDecimals;
        indicator.FlatTolerance = 0;

        Assert.Null(IndicatorRule.Validate(indicator));
    }

    [Fact]
    public void Validate_AcceptsEqualRangeBounds()
    {
        var indicator = Valid();
        indicator.RangeMin = 5;
        indicator.RangeMax = 5;

        Assert.Null(IndicatorRule.Validate(indicator));
    }

    /// <summary>BR-030 必填字段，以及小数位、持平容差、上下限、标准差倍数的范围；Field 指向出错的表单字段</summary>
    [Theory]
    [MemberData(nameof(InvalidCases))]
    public void Validate_ReportsTheInvalidField(string field, Action<Indicator> breakIt)
    {
        var indicator = Valid();
        breakIt(indicator);

        Assert.Equal(field, IndicatorRule.Validate(indicator)?.Field);
    }

    public static TheoryData<string, Action<Indicator>> InvalidCases => new()
    {
        { "NameZh", i => i.Name = new BilingualText(" ", "Nonfarm Payrolls") },
        { "NameEn", i => i.Name = new BilingualText("非农就业人数", "") },
        { "ShortNameZh", i => i.ShortName = new BilingualText("", "NFP") },
        { "AgencyEn", i => i.Agency = new BilingualText("美国劳工统计局", " ") },
        { "Category", i => i.Category = default },
        { "ValueType", i => i.ValueType = (IndicatorValueType)99 },
        { "Unit", i => i.Unit = default },
        { "Frequency", i => i.Frequency = default },
        { "Importance", i => i.Importance = default },
        { "Direction", i => i.Direction = default },
        { "Decimals", i => i.Decimals = -1 },
        { "Decimals", i => i.Decimals = IndicatorRule.MaxDecimals + 1 },
        { "FlatTolerance", i => i.FlatTolerance = -0.000001m },
        { "RangeMin", i => (i.RangeMin, i.RangeMax) = (10, 9.999999m) },
        { "SigmaN", i => i.SigmaN = 0 },
        { "SigmaN", i => i.SigmaN = -1 },
    };

    /// <summary>BR-031：影响方向改变时必须填写理由</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void DirectionChange_RequiresReason(string? reason)
        => Assert.NotNull(IndicatorRule.ValidateDirectionChange(ImpactDirection.HigherIsBullish, ImpactDirection.Neutral, reason));

    [Fact]
    public void DirectionChange_AcceptsReason()
        => Assert.Null(IndicatorRule.ValidateDirectionChange(ImpactDirection.HigherIsBullish, ImpactDirection.HigherIsBearish, "口径调整"));

    [Fact]
    public void DirectionUnchanged_NeedsNoReason()
        => Assert.Null(IndicatorRule.ValidateDirectionChange(ImpactDirection.Neutral, ImpactDirection.Neutral, null));

    private static Indicator Valid() => new()
    {
        Code = "US.NFP",
        EconomyCode = "US",
        CurrencyCode = "USD",
        Name = new BilingualText("非农就业人数", "Nonfarm Payrolls"),
        ShortName = new BilingualText("非农", "NFP"),
        Category = IndicatorCategory.Employment,
        ValueType = IndicatorValueType.Value,
        Unit = IndicatorUnit.ThousandPersons,
        Decimals = 0,
        Frequency = ReleaseFrequency.Monthly,
        Importance = Importance.High,
        Direction = ImpactDirection.HigherIsBullish,
        FlatTolerance = 10,
        RangeMin = -1000,
        RangeMax = 1000,
        Agency = new BilingualText("美国劳工统计局", "Bureau of Labor Statistics"),
        DefaultReleaseTime = new TimeOnly(8, 30),
        Description = new BilingualText("非农业部门新增就业人数", "Change in nonfarm employment"),
    };
}
