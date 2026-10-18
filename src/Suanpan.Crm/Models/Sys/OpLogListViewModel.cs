using Suanpan.Services.Sys.Logging.Models;
using Suanpan.Shared.Paging;

namespace Suanpan.Crm.Models.Sys;

public sealed class OpLogListViewModel
{
    public required OpLogFilter Filter { get; init; }

    public required PagedResult<OpLogRow> Result { get; init; }

    /// <summary>筛选下拉框的选项：代码中标了 [OpLog] 的模块和动作，按模块分组</summary>
    public required IReadOnlyList<IGrouping<string, string>> ActionsByModule { get; init; }
}
