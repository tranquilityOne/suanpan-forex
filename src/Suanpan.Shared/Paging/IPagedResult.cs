namespace Suanpan.Shared.Paging;

/// <summary>分页信息（不含数据行），供分页部件等不关心行类型的地方使用</summary>
public interface IPagedResult
{
    int Total { get; }

    int Page { get; }

    int PageSize { get; }

    int TotalPages { get; }

    bool HasPrev { get; }

    bool HasNext { get; }
}
