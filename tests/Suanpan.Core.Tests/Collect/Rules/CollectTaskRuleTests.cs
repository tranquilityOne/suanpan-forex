using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Parsers;
using Suanpan.Core.Collect.Rules;
using Xunit;

namespace Suanpan.Core.Tests.Collect.Rules;

public class CollectTaskRuleTests
{
    private static readonly ParserDescriptor Schedule = new("us.bls.schedule", "BLS 发布日程", CollectTargetType.Calendar);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{\"series\": [\"CES0000000001\"]}")]
    public void Validate_AcceptsKnownParserWithEmptyOrObjectParams(string? parameters)
    {
        var task = Valid();
        task.Params = parameters;

        Assert.Null(CollectTaskRule.Validate(task, Find));
    }

    /// <summary>BR-012：解析器必须在目录中；目标数据类型跟随解析器；参数必须是 JSON 对象</summary>
    [Theory]
    [MemberData(nameof(InvalidCases))]
    public void Validate_ReportsTheInvalidField(string field, Action<CollectTask> breakIt)
    {
        var task = Valid();
        breakIt(task);

        Assert.Equal(field, CollectTaskRule.Validate(task, Find)?.Field);
    }

    public static TheoryData<string, Action<CollectTask>> InvalidCases => new()
    {
        { "Name", t => t.Name = " " },
        { "ParserKey", t => t.ParserKey = "us.bls.unknown" },
        { "ParserKey", t => t.TargetType = CollectTargetType.Release },
        { "Params", t => t.Params = "[1, 2]" },
        { "Params", t => t.Params = "{not json" },
    };

    /// <summary>PRD §13.1.2：编辑只能触发日历排期、公布值类任务</summary>
    [Theory]
    [InlineData(CollectTargetType.Calendar, true)]
    [InlineData(CollectTargetType.Release, true)]
    [InlineData(CollectTargetType.History, false)]
    [InlineData(CollectTargetType.News, false)]
    public void IsEditorTriggerable_OnlyCalendarAndRelease(CollectTargetType targetType, bool expected)
        => Assert.Equal(expected, CollectTaskRule.IsEditorTriggerable(targetType));

    private static ParserDescriptor? Find(string key) => key == Schedule.Key ? Schedule : null;

    private static CollectTask Valid() => new()
    {
        Name = "BLS 发布日程",
        SourceId = 1,
        ParserKey = Schedule.Key,
        TargetType = CollectTargetType.Calendar,
        Cron = "0 1 * * *",
    };
}
