namespace Suanpan.Infrastructure.Tests.Fixtures;

/// <summary>
/// 测试分组（docs/conventions/backend.md §13）：要启动容器的测试类标
/// [Trait(TestCategory.Key, TestCategory.Integration)]，本地可以只跑单元测试。
/// </summary>
public static class TestCategory
{
    public const string Key = "Category";

    public const string Integration = "Integration";
}
