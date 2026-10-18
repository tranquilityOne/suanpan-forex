namespace Suanpan.Shared.Paging;

/// <summary>通用分页结果</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize) : IPagedResult
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);

    public bool HasPrev => Page > 1;

    public bool HasNext => Page < TotalPages;
}
