using Suanpan.Core.Reference.Entities;
using Suanpan.Services.Reference.Currencies.Models;
using Suanpan.Shared.Mvc;

namespace Suanpan.Services.Reference.Currencies;

/// <summary>货币（基础数据设计 §3.3、§3.5、§5.2，FR-005）</summary>
public interface ICurrencyService
{
    /// <summary>全部货币，按编码排序</summary>
    Task<IReadOnlyList<CurrencyRow>> GetListAsync(CancellationToken ct = default);

    Task<Currency?> GetAsync(string code, CancellationToken ct = default);

    /// <summary>下拉框选项：只有启用的货币（BR-028：停用后不能再被新选用）</summary>
    Task<IReadOnlyList<Currency>> GetOptionsAsync(CancellationToken ct = default);

    /// <summary>编码先去空白、转大写；格式不对或已存在时失败，Field 为 Code（ERR-REF-001）</summary>
    Task<ManageResult> CreateAsync(CurrencyInput input, CancellationToken ct = default);

    /// <summary>只改名称，编码不可修改</summary>
    Task<ManageResult> UpdateAsync(CurrencyInput input, CancellationToken ct = default);

    Task<ManageResult> SetEnabledAsync(string code, bool enabled, CancellationToken ct = default);

    /// <summary>被经济体或指标引用时拒绝，提示中列出引用方（BR-028、ERR-REF-002）</summary>
    Task<ManageResult> DeleteAsync(string code, CancellationToken ct = default);
}
