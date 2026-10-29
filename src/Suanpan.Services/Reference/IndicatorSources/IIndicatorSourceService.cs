using Suanpan.Core.Reference.Entities;
using Suanpan.Services.Reference.IndicatorSources.Models;
using Suanpan.Shared.Mvc;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Reference.IndicatorSources;

/// <summary>
/// 指标与数据源的映射（基础数据设计 §3.8、§5.2，BR-034）。用途分公布值和排期：公布值映射有主源和备源，主源失败时改用备源由采集模块实现；
/// 排期映射用发布标识匹配指标（日历设计 §4.2）
/// </summary>
public interface IIndicatorSourceService
{
    /// <summary>按指标编码排序，同一指标先列公布值映射（按优先级），再列排期映射</summary>
    Task<PagedResult<IndicatorSourceRow>> QueryAsync(IndicatorSourceQuery query, CancellationToken ct = default);

    /// <summary>一个指标的全部映射，排序同列表；指标详情页使用</summary>
    Task<IReadOnlyList<IndicatorSourceRow>> GetByIndicatorAsync(string indicatorCode, CancellationToken ct = default);

    Task<IndicatorSource?> GetAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// 指标必须存在且启用（BR-028）；数据源停用时照样保存，由页面提示（ERR-IND-003）。失败时 Field 指向出错的字段：
    /// 字段规则（IndicatorSourceRule）、解析器不在目录中或目标数据类型与用途不符、数据源不存在、
    /// 同一指标对同一数据源重复映射同一用途、公布值映射的优先级重复
    /// </summary>
    Task<ManageResult> CreateAsync(IndicatorSourceInput input, CancellationToken ct = default);

    /// <summary>指标和用途不能改（输入中的这两项不使用）；其余校验同新增</summary>
    Task<ManageResult> UpdateAsync(long id, IndicatorSourceInput input, CancellationToken ct = default);

    Task<ManageResult> DeleteAsync(long id, CancellationToken ct = default);
}
