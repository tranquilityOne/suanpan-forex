using Suanpan.Core.Collect.Parsers;

namespace Suanpan.Core.Collect.Rules;

/// <summary>
/// 重新解析的规则（采集设计 §5.6，FR-004 主流程第 3 步）：按任务进行，可以改选同一适配器下的其他解析器；
/// 一次最多 5000 个快照（BR-024，ERR-PARSE-004）。方法返回 null 表示通过，否则返回提示
/// </summary>
public static class ReparseRule
{
    public const int MaxSnapshots = 5000;

    /// <summary>
    /// 解析器必须在目录中，与任务的解析器属于同一适配器（快照是这个适配器请求的）、目标数据类型相同（交给同一个入库服务）
    /// </summary>
    public static string? ValidateParser(ParserDescriptor? taskParser, ParserDescriptor? chosen)
    {
        if (chosen is null)
        {
            return "解析器不存在";
        }

        if (taskParser is null || chosen.AdapterKey != taskParser.AdapterKey)
        {
            return $"只能选择与任务的解析器同一适配器的解析器（{taskParser?.AdapterKey ?? "任务的解析器已不在目录中"}）";
        }

        return chosen.TargetType != taskParser.TargetType ? "解析器的目标数据类型与任务不同" : null;
    }

    public static string? ValidateCount(int count) => count switch
    {
        0 => "没有选中快照",
        > MaxSnapshots => $"选中了 {count} 个快照，单次重新解析最多 {MaxSnapshots} 个，请缩小筛选范围分批执行（ERR-PARSE-004）",
        _ => null,
    };
}
