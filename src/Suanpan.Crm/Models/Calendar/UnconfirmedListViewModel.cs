using Suanpan.Services.Calendar.Events.Models;
using Suanpan.Shared.Paging;

namespace Suanpan.Crm.Models.Calendar;

public sealed class UnconfirmedListViewModel
{
    public required UnconfirmedFilter Filter { get; init; }

    public required PagedResult<UnconfirmedEventRow> Result { get; init; }
}
