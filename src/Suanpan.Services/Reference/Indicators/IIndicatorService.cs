using Suanpan.Core.Reference.Entities;
using Suanpan.Services.Reference.Indicators.Models;
using Suanpan.Shared.Mvc;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Reference.Indicators;

/// <summary>指标字典（基础数据设计 §3.4、§3.5、§5.2，FR-006）</summary>
public interface IIndicatorService
{
    /// <summary>按经济体的显示顺序、指标编码排序</summary>
    Task<PagedResult<IndicatorRow>> QueryAsync(IndicatorQuery query, CancellationToken ct = default);

    /// <summary>下拉框选项：只有启用的指标，按经济体的显示顺序、指标编码排序</summary>
    Task<IReadOnlyList<Indicator>> GetOptionsAsync(CancellationToken ct = default);

    Task<Indicator?> GetAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// 编码先去空白、转大写；失败时 Field 指向出错的字段：经济体不存在或已停用、编码格式不对或已存在（ERR-IND-001）、
    /// 货币不存在或已停用、字段规则（IndicatorRule）
    /// </summary>
    Task<ManageResult> CreateAsync(IndicatorInput input, CancellationToken ct = default);

    /// <summary>
    /// 编码和所属经济体不可修改；换成别的货币时新货币必须启用；影响方向变化时必须填写理由（BR-031，ERR-IND-002），
    /// 理由由操作日志过滤器从请求参数中采集
    /// </summary>
    Task<ManageResult> UpdateAsync(IndicatorInput input, CancellationToken ct = default);

    Task<ManageResult> SetEnabledAsync(string code, bool enabled, CancellationToken ct = default);

    /// <summary>被引用时拒绝，只能停用（BR-153）；没有被引用的可以删除</summary>
    Task<ManageResult> DeleteAsync(string code, CancellationToken ct = default);
}
