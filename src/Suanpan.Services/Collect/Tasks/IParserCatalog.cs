using Suanpan.Core.Collect.Parsers;

namespace Suanpan.Services.Collect.Tasks;

/// <summary>
/// 解析器目录（采集设计 §5.2，BR-012）。实际就是 Core 的 <see cref="ParserCatalog"/>；做成接口是为了在测试中换成假的解析器
/// （M1 目录为空，没有它就测不到保存任务的成功路径）。Collector 按标识找实现时直接用 ParserCatalog
/// </summary>
public interface IParserCatalog
{
    IReadOnlyList<ParserDescriptor> All { get; }

    /// <summary>按标识查找；不存在时返回 null</summary>
    ParserDescriptor? Find(string? key);
}

public sealed class DefaultParserCatalog : IParserCatalog
{
    public IReadOnlyList<ParserDescriptor> All => ParserCatalog.All;

    public ParserDescriptor? Find(string? key) => ParserCatalog.Find(key);
}
