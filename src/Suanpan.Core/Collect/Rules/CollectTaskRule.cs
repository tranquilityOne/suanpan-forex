using System.Text.Json;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Parsers;
using Suanpan.Core.Common.Validation;

namespace Suanpan.Core.Collect.Rules;

/// <summary>
/// 采集任务保存时的校验（采集设计 §3.2，BR-007、BR-012）。cron 的写法由 Services 中的 CronSchedule 校验（Core 不引用第三方包）；
/// 名称是否重复、数据源是否存在要查库，由服务校验。
/// </summary>
public static class CollectTaskRule
{
    /// <summary>
    /// 名称必填；解析器必须在目录中，目标数据类型跟随解析器（BR-012）；参数为空或是 JSON 对象。
    /// findParser 一般传 <see cref="ParserCatalog.Find"/>。返回第一条错误，全部通过时返回 null
    /// </summary>
    public static FieldError? Validate(CollectTask task, Func<string, ParserDescriptor?> findParser)
    {
        if (string.IsNullOrWhiteSpace(task.Name))
        {
            return new(nameof(CollectTask.Name), "请填写名称");
        }

        if (findParser(task.ParserKey) is not { } parser)
        {
            return new(nameof(CollectTask.ParserKey), "请选择解析器");
        }

        if (task.TargetType != parser.TargetType)
        {
            return new(nameof(CollectTask.ParserKey), "目标数据类型与解析器不一致");
        }

        return IsJsonObjectOrEmpty(task.Params) ? null : new(nameof(CollectTask.Params), "参数必须是 JSON 对象，如 {\"series\": [\"CES0000000001\"]}");
    }

    /// <summary>
    /// 编辑只能手动触发"排期采集、公布值补采"类任务（PRD §13.1.2，采集设计 §8.2），即目标数据类型为日历排期、公布值的任务；
    /// 有"立即执行（全部）"权限的角色不受这个限制
    /// </summary>
    public static bool IsEditorTriggerable(CollectTargetType targetType)
        => targetType is CollectTargetType.Calendar or CollectTargetType.Release;

    private static bool IsJsonObjectOrEmpty(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
