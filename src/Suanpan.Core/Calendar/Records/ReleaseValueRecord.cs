using Suanpan.Core.Reference.Enums;

namespace Suanpan.Core.Calendar.Records;

/// <summary>
/// 公布值解析器（目标数据类型"公布值"）输出的一期数值（日历设计 §5.1），交给公布值入库。
/// 解析器每次输出数据中能取到的各期，入库服务据此判断最新一期和修正（BR-044、BR-046）
/// </summary>
/// <param name="ExternalId">外部标识，对应公布值映射（BR-034）；BLS 的写法为 {序列 ID} 或 {序列 ID}:{计算方式}</param>
/// <param name="ReferencePeriod">参考期，按 ReferencePeriodRule 的写法</param>
/// <param name="Value">未舍入的值，入库时按指标的小数位四舍五入（BR-043）</param>
/// <param name="ValueUpper">区间型指标的上限；数值型为空</param>
/// <param name="Unit">这个值的单位；与指标的单位不同时该条入库失败（ERR-PARSE-006）</param>
/// <param name="Raw">原始字符串：官方直接给出的数值原样记录，计算得到的记参与计算的序列和数值（BR-043）</param>
/// <param name="ResponseIndex">来自第几个响应，对应入库请求中的快照 id（采集设计 §5.3）</param>
public sealed record ReleaseValueRecord(
    string ExternalId,
    string ReferencePeriod,
    decimal Value,
    decimal? ValueUpper,
    IndicatorUnit Unit,
    string Raw,
    int ResponseIndex);
