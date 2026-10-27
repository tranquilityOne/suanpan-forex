namespace Suanpan.Collector.Execution;

/// <summary>
/// 适配器（采集设计 §5.1）：一个数据源一个，负责"怎么请求"——拼地址、放凭据、分页。只发请求，不解析、不写库；
/// 请求一律通过 <see cref="FetchContext.SendAsync"/> 发出，频率限制、超时、User-Agent 由框架统一处理，重试由执行器处理
/// </summary>
public interface ICollectAdapter
{
    /// <summary>适配器标识，与解析器标识的前两段一致，如 us.bls</summary>
    string Key { get; }

    Task<FetchResult> FetchAsync(FetchContext context, CancellationToken ct);
}
