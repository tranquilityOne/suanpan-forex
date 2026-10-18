using Suanpan.Services.Sys.Logging.Models;
using Suanpan.Shared.Paging;

namespace Suanpan.Crm.Models.Sys;

public sealed class LoginLogListViewModel
{
    public required LoginLogFilter Filter { get; init; }

    public required PagedResult<LoginLogRow> Result { get; init; }
}
