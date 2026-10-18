using Microsoft.EntityFrameworkCore;
using Suanpan.Shared.Paging;

namespace Suanpan.Services.Common.Querying;

public static class PagingExtensions
{
    public const int MaxPageSize = 200;

    /// <summary>分页查询：页码从 1 开始，每页条数限制在 1 ~ <see cref="MaxPageSize"/>；调用方先排好序</summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<T>(items, total, page, pageSize);
    }
}
