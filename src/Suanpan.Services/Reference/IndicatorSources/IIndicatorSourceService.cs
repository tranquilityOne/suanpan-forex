using Suanpan.Core.Reference.Entities;
using Suanpan.Services.Reference.IndicatorSources.Models;
using Suanpan.Shared.Mvc;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Reference.IndicatorSources;

/// <summary>指标与数据源的映射（基础数据设计 §3.8、§5.2，BR-034）。主源失败时改用备源由采集模块实现</summary>
public interface IIndicatorSourceService
{
    /// <summary>按指标编码、优先级排序</summary>
    Task<PagedResult<IndicatorSourceRow>> QueryAsync(IndicatorSourceQuery query, CancellationToken ct = default);

    /// <summary>一个指标的全部映射，按优先级排序；指标详情页使用</summary>
    Task<IReadOnlyList<IndicatorSourceRow>> GetByIndicatorAsync(string indicatorCode, CancellationToken ct = default);

    Task<IndicatorSource?> GetAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// 指标必须存在且启用（BR-028）；数据源停用时照样保存，由页面提示（ERR-IND-003）。失败时 Field 指向出错的字段：
    /// 字段规则（IndicatorSourceRule）、解析器不在目录中、数据源不存在、同一指标重复映射同一数据源、优先级重复
    /// </summary>
    Task<ManageResult> CreateAsync(IndicatorSourceInput input, CancellationToken ct = default);

    /// <summary>指标不能改；其余校验同新增</summary>
    Task<ManageResult> UpdateAsync(long id, IndicatorSourceInput input, CancellationToken ct = default);

    Task<ManageResult> DeleteAsync(long id, CancellationToken ct = default);
}
