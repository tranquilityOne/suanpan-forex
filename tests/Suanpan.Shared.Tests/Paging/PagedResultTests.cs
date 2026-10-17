using Suanpan.Shared.Paging;
using Xunit;

namespace Suanpan.Shared.Tests.Paging;

public class PagedResultTests
{
    [Theory]
    [InlineData(0, 1, 20, 0, false, false)]
    [InlineData(20, 1, 20, 1, false, false)]
    [InlineData(21, 1, 20, 2, false, true)]
    [InlineData(21, 2, 20, 2, true, false)]
    [InlineData(5, 1, 0, 0, false, false)]
    public void Pages_AreComputedFromTotalAndPageSize(int total, int page, int pageSize, int totalPages, bool hasPrev, bool hasNext)
    {
        var result = new PagedResult<int>([], total, page, pageSize);

        Assert.Equal(totalPages, result.TotalPages);
        Assert.Equal(hasPrev, result.HasPrev);
        Assert.Equal(hasNext, result.HasNext);
    }
}
