namespace Suanpan.Collector.Execution;

/// <summary>
/// 解析器（采集设计 §5.2，硬性规则 14）：纯函数，输入响应内容和任务参数，输出标准化记录；不访问数据库和网络，
/// 配基于真实快照的测试。内容不符合预期时抛出 <see cref="ParseException"/>
/// </summary>
public interface IParser
{
    /// <summary>解析器目录（ParserCatalog）中的标识，如 us.bls.release</summary>
    string Key { get; }

    ParseResult Parse(FetchResult fetched, string? parameters);
}

/// <summary>解析结果：记录交给入库服务；Missing 为应有却没找到的内容（如某个指标），不为空时运行记为部分成功（ERR-PARSE-002）</summary>
public sealed record ParseResult(IReadOnlyList<object> Records, IReadOnlyList<string> Missing);

/// <summary>解析失败（ERR-PARSE-001）：运行记为失败，不入库、不重试</summary>
public sealed class ParseException(string message) : Exception(message);
