using Suanpan.Core.Collect.Enums;

namespace Suanpan.Core.Collect.Parsers;

/// <summary>
/// 全部解析器的目录（采集设计 §5.2，BR-012）：Crm 用它填下拉框、校验任务配置，Collector 按标识找到实现。
/// 放在 Core 中是因为 Crm 不引用 Collector。新增解析器时在这里登记，Suanpan.Collector.Tests 检查目录与实现一一对应
/// </summary>
public static class ParserCatalog
{
    public static IReadOnlyList<ParserDescriptor> All { get; } =
    [
        // 美国劳工统计局（ENG-009.1）：发布日程来自官网，公布值来自 API v2
        new("us.blsweb.schedule", "BLS 发布日程", CollectTargetType.Calendar),
        new("us.blsapi.release", "BLS 公布值（API v2）", CollectTargetType.Release),
    ];

    /// <summary>按标识查找；不存在时返回 null</summary>
    public static ParserDescriptor? Find(string? key)
        => All.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.Ordinal));
}
