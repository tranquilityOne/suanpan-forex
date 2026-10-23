using Suanpan.Core.Collect.Entities;
using Suanpan.Services.Collect.Sources.Models;

namespace Suanpan.Crm.Models.Collect;

public sealed class DataSourceDetailViewModel
{
    /// <summary>凭据为密文，页面只显示是否已设置</summary>
    public required DataSource Source { get; init; }

    /// <summary>所属经济体的中文名；没填或查不到时为空</summary>
    public string? EconomyName { get; init; }

    /// <summary>刚执行的连通性测试结果（经 TempData 带到详情页）</summary>
    public DataSourceTestResult? TestResult { get; init; }
}
