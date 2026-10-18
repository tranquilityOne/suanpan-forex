using Suanpan.Core.Sys.Enums;
using Suanpan.Shared.Paging;

namespace Suanpan.Crm.Models.Dev;

/// <summary>公共部件示例页</summary>
public sealed class DevComponentsViewModel
{
    public required PagedResult<DevSampleRow> Rows { get; init; }

    public required DevSampleFormViewModel Form { get; init; }
}

public sealed record DevSampleRow(long Id, string Name, UserStatus Status, DateTime CreatedAt);
