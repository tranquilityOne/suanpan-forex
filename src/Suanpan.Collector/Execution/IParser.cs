namespace Suanpan.Collector.Execution;

/// <summary>
/// 解析器（采集设计 §5.2，硬性规则 14）：纯函数，输入响应内容和任务参数，输出标准化记录；不访问数据库和网络，
/// 配基于真实快照的测试。内容不符合预期时抛出 <see cref="ParseException"/>
/// </summary>
public interface IParser
{
    /// <summary>解析器目录（ParserCatalog）中的标识，如 us.blsapi.release</summary>
    string Key { get; }

    ParseResult Parse(FetchResult fetched, string? parameters);
}

/// <summary>解析结果：记录交给入库服务；Missing 为应有却没找到的内容（如某个指标），不为空时运行记为部分成功（ERR-PARSE-002）</summary>
public sealed record ParseResult(IReadOnlyList<object> Records, IReadOnlyList<string> Missing);

/// <summary>解析失败（ERR-PARSE-001）：运行记为失败，不入库、不重试</summary>
public class ParseException(string message) : Exception(message);

/// <summary>
/// 快照无效（ERR-PARSE-003，采集设计 §5.5）：内容不是数据而是拦截页（如"Access Denied"页面）等。
/// 执行器把这次请求的快照标记为无效（不参与重新解析），运行记为失败；Message 为原因
/// </summary>
public sealed class InvalidSnapshotException(string message) : ParseException(message);
