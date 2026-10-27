using System.Xml.Linq;
using Xunit;

namespace Suanpan.Core.Tests.Architecture;

/// <summary>
/// 依赖方向（后端约定 §2，CLAUDE.md 硬性规则 1、2）：读 src/ 下各项目的 csproj，检查项目引用只能向下、Core 零依赖、
/// Portal 不引用 Services、程序项目不直接引用第三方包。违反时代码评审不放行，这里让它在测试中就失败。
/// 读的是源码中的项目文件，不依赖编译结果，所以放在不引用其他项目的 Core 测试中
/// </summary>
public class DependencyDirectionTests
{
    /// <summary>各项目所在的层，引用只能指向更低的层；同一层的项目（Data 与 Infrastructure、四个程序）不能互相引用</summary>
    private static readonly Dictionary<string, int> Layers = new(StringComparer.Ordinal)
    {
        ["Suanpan.Core"] = 0,
        ["Suanpan.Shared"] = 1,
        ["Suanpan.Data"] = 2,
        ["Suanpan.Infrastructure"] = 2,
        ["Suanpan.Services"] = 3,
        ["Suanpan.Collector"] = 4,
        ["Suanpan.Job"] = 4,
        ["Suanpan.Crm"] = 4,
        ["Suanpan.Portal"] = 4,
    };

    private static readonly IReadOnlyList<ProjectFile> Projects = ProjectFile.LoadAll();

    /// <summary>新增项目时要在层级表中登记，否则下面的规则管不到它</summary>
    [Fact]
    public void EveryProjectHasALayer()
        => Assert.Equal(Layers.Keys.Order(StringComparer.Ordinal), Projects.Select(p => p.Name).Order(StringComparer.Ordinal));

    [Fact]
    public void ProjectReferencesOnlyPointDown()
    {
        var violations = Projects
            .SelectMany(p => p.ProjectReferences.Select(target => (p.Name, Target: target)))
            .Where(r => !Layers.TryGetValue(r.Target, out var targetLayer) || targetLayer >= Layers[r.Name])
            .Select(r => $"{r.Name} → {r.Target}");

        Assert.Empty(violations);
    }

    /// <summary>Core 零依赖：不引用其他项目、NuGet 包和 ASP.NET Core 等框架，领域规则只用 .NET 基础库</summary>
    [Fact]
    public void CoreHasNoDependencies()
    {
        var core = Projects.Single(p => p.Name == "Suanpan.Core");

        Assert.Equal("Microsoft.NET.Sdk", core.Sdk);
        Assert.Empty(core.ProjectReferences.Concat(core.PackageReferences).Concat(core.FrameworkReferences));
    }

    /// <summary>前台只读不写（PRD DEC-021、BR-121）：Portal 只通过对外视图的只读上下文查数据</summary>
    [Fact]
    public void PortalDoesNotReferenceServices()
        => Assert.DoesNotContain("Suanpan.Services", Projects.Single(p => p.Name == "Suanpan.Portal").ProjectReferences);

    /// <summary>程序项目（Web SDK）不直接引用第三方包，第三方包都在类库中引用</summary>
    [Fact]
    public void ProgramsHaveNoPackageReferences()
    {
        var violations = Projects
            .Where(p => p.Sdk == "Microsoft.NET.Sdk.Web")
            .SelectMany(p => p.PackageReferences.Select(package => $"{p.Name} → {package}"));

        Assert.Empty(violations);
    }

    private sealed record ProjectFile(
        string Name, string Sdk, IReadOnlyList<string> ProjectReferences, IReadOnlyList<string> PackageReferences, IReadOnlyList<string> FrameworkReferences)
    {
        public static IReadOnlyList<ProjectFile> LoadAll()
            => Directory.GetFiles(Path.Combine(RepositoryRoot(), "src"), "*.csproj", SearchOption.AllDirectories)
                .Select(Load)
                .ToList();

        private static ProjectFile Load(string path)
        {
            var document = XDocument.Load(path);
            IReadOnlyList<string> Includes(string element) => document.Descendants(element)
                .Select(e => (string?)e.Attribute("Include") ?? string.Empty)
                .ToList();

            return new ProjectFile(
                Path.GetFileNameWithoutExtension(path),
                (string?)document.Root!.Attribute("Sdk") ?? string.Empty,
                // 项目文件里的路径用反斜杠，Linux 上不认作分隔符，先统一成正斜杠
                Includes("ProjectReference").Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/'))).ToList(),
                Includes("PackageReference"),
                Includes("FrameworkReference"));
        }

        /// <summary>从测试程序所在目录往上找解决方案文件</summary>
        private static string RepositoryRoot()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Suanpan.sln")))
                {
                    return directory.FullName;
                }
            }

            throw new InvalidOperationException($"从 {AppContext.BaseDirectory} 往上没有找到 Suanpan.sln");
        }
    }
}
