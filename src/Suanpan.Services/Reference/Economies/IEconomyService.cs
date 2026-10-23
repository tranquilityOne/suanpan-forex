using Suanpan.Core.Reference.Entities;
using Suanpan.Services.Reference.Economies.Models;
using Suanpan.Shared.Mvc;

namespace Suanpan.Services.Reference.Economies;

/// <summary>经济体（基础数据设计 §3.3、§3.5、§5.2，FR-005）</summary>
public interface IEconomyService
{
    /// <summary>全部经济体，按显示顺序排序，带各自的指标数、数据源数</summary>
    Task<IReadOnlyList<EconomyRow>> GetListAsync(CancellationToken ct = default);

    Task<Economy?> GetAsync(string code, CancellationToken ct = default);

    /// <summary>下拉框选项：只有启用的经济体（BR-028）</summary>
    Task<IReadOnlyList<Economy>> GetOptionsAsync(CancellationToken ct = default);

    /// <summary>新增时排在最后的显示顺序：当前最大值加 10</summary>
    Task<short> GetNextSortAsync(CancellationToken ct = default);

    /// <summary>
    /// 编码先去空白、转大写；失败时 Field 指向出错的字段：编码格式不对或已存在（ERR-REF-001）、
    /// 货币不存在或已停用、时区不是 IANA 名称（ERR-REF-003）
    /// </summary>
    Task<ManageResult> CreateAsync(EconomyInput input, CancellationToken ct = default);

    /// <summary>编码不可修改；货币没有变化时可以保留已停用的货币，换成别的货币时新货币必须启用</summary>
    Task<ManageResult> UpdateAsync(EconomyInput input, CancellationToken ct = default);

    Task<ManageResult> SetEnabledAsync(string code, bool enabled, CancellationToken ct = default);

    /// <summary>被指标或数据源引用时拒绝，提示中列出引用方（BR-028、ERR-REF-002）</summary>
    Task<ManageResult> DeleteAsync(string code, CancellationToken ct = default);
}
