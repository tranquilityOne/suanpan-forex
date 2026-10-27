using System.Reflection;
using Xunit;

namespace Suanpan.Services.Tests.Fixtures;

/// <summary>
/// 后端约定 §13.4：本项目的程序集夹具（[assembly: AssemblyFixture]）都要启动容器，构造函数用到它们的测试类必须标
/// [Trait(TestCategory.Key, TestCategory.Integration)]，只跑单元测试时才不会启动容器、不需要 Docker。
/// 夹具从程序集特性中读取，以后新增的夹具自动纳入检查
/// </summary>
public class ContainerFixtureUsageTests
{
    [Fact]
    public void TestClassesUsingContainerFixtures_AreMarkedIntegration()
    {
        var assembly = typeof(ContainerFixtureUsageTests).Assembly;
        var fixtures = assembly.CustomAttributes
            .Where(a => a.AttributeType == typeof(AssemblyFixtureAttribute))
            .Select(a => (Type)a.ConstructorArguments[0].Value!)
            .ToHashSet();

        var unmarked = assembly.GetTypes()
            .Where(t => t.GetConstructors().Any(c => c.GetParameters().Any(p => fixtures.Contains(p.ParameterType))))
            .Where(t => !t.CustomAttributes.Any(IsIntegrationTrait))
            .Select(t => t.FullName);

        Assert.NotEmpty(fixtures);
        Assert.Empty(unmarked);
    }

    private static bool IsIntegrationTrait(CustomAttributeData attribute)
        => attribute.AttributeType == typeof(TraitAttribute)
           && attribute.ConstructorArguments is [{ Value: TestCategory.Key }, { Value: TestCategory.Integration }];
}
