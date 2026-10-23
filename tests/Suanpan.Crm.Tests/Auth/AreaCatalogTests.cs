using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Suanpan.Crm.Auth;
using Suanpan.Crm.Tests.Fixtures;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;
using Xunit;

namespace Suanpan.Crm.Tests.Auth;

/// <summary>栏目清单和 Area 内页面的约定（sys 设计 §6.2、§9.1）</summary>
public partial class AreaCatalogTests
{
    /// <summary>权限树重建只为栏目清单中的 Area 建目录；漏登记的 Area，页面不会出现在权限树和菜单中</summary>
    [Fact]
    public async Task EveryAreaWithPagesIsInCatalog()
    {
        await using var app = new CrmApp();
        var catalog = AreaCatalog.All.Select(a => a.Area).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var areas = AreaActions(app).Select(d => d.RouteValues["area"]).Distinct().ToList();

        Assert.NotEmpty(areas);
        Assert.All(areas, area => Assert.Contains(area!, catalog));
    }

    /// <summary>Area 内的 Controller 必须标 [PermMenu]，否则重建出的菜单没有中文名和图标</summary>
    [Fact]
    public async Task EveryAreaControllerHasPermMenu()
    {
        await using var app = new CrmApp();

        var controllers = AreaActions(app).Select(d => d.ControllerTypeInfo).Distinct().ToList();

        Assert.All(controllers, type => Assert.NotNull(type.GetCustomAttribute<PermMenuAttribute>()));
    }

    /// <summary>
    /// 各栏目的权限码与模块设计文档一致：系统管理见 sys 设计 §9.1，基础数据见基础数据设计 §6
    /// （权限码由 Area、Controller、Action 名自动生成，改名会改变权限码）
    /// </summary>
    [Fact]
    public async Task PagesHaveDocumentedPermCodes()
    {
        await using var app = new CrmApp();
        string[] documented =
        [
            "sys:user:index", "sys:user:create", "sys:user:edit", "sys:user:set_status",
            "sys:user:reset_password", "sys:user:reset_totp", "sys:user:unlock",
            "sys:role:index", "sys:role:create", "sys:role:edit", "sys:role:delete", "sys:role:assign", "sys:role:apply_defaults",
            "sys:permission:index", "sys:permission:edit", "sys:permission:rebuild",
            "sys:config:index", "sys:config:edit", "sys:config:reset",
            "sys:ip_whitelist:index", "sys:ip_whitelist:create", "sys:ip_whitelist:edit", "sys:ip_whitelist:delete",
            "sys:op_log:index", "sys:op_log:export", "sys:login_log:index", "sys:login_log:export",
            "sys:job:index",
            "reference:economy:index", "reference:economy:create", "reference:economy:edit", "reference:economy:set_status", "reference:economy:delete",
            "reference:currency:index", "reference:currency:create", "reference:currency:edit", "reference:currency:set_status", "reference:currency:delete",
        ];

        var codes = GeneratedPermCodes(app);

        Assert.All(documented, code => Assert.Contains(code, codes));
    }

    /// <summary>
    /// 视图里按权限隐藏按钮用的权限码（asp-permission、CurrentPermissions.HasAsync）必须是代码生成的权限码。
    /// 写错时按钮对所有非管理员都隐藏，管理员却照常看得到，不容易发现。
    /// </summary>
    [Fact]
    public async Task PermCodesUsedInViewsExist()
    {
        await using var app = new CrmApp();
        var codes = GeneratedPermCodes(app);
        var crmDir = Path.Combine(RepositoryRoot(), "src", "Suanpan.Crm");

        var used = Directory.EnumerateFiles(crmDir, "*.cshtml", SearchOption.AllDirectories)
            .SelectMany(file => ViewPermCode().Matches(File.ReadAllText(file))
                .Select(m => (File: Path.GetRelativePath(crmDir, file), Code: m.Groups["code"].Value)))
            .ToList();

        Assert.NotEmpty(used);
        Assert.All(used, u => Assert.True(codes.Contains(u.Code), $"{u.File} 中的权限码 {u.Code} 不存在"));
    }

    /// <summary>
    /// 硬性规则 8、BR-150：Area 内的 POST 都带防伪校验和 [OpLog]，导出（Export 开头的 action）也带 [OpLog]。
    /// 漏标的写操作不会留下操作日志，过滤器不会报错，只能靠这里发现。
    /// </summary>
    [Fact]
    public async Task WriteAndExportActionsAreProtectedAndOpLogged()
    {
        await using var app = new CrmApp();

        // 只查 Crm 自己的页面，测试中加载的探针页面不算
        var actions = AreaActions(app)
            .Where(d => d.ControllerTypeInfo.Assembly == typeof(Program).Assembly)
            .Where(d => IsPost(d) || d.ActionName.StartsWith("Export", StringComparison.Ordinal))
            .ToList();

        Assert.Contains(actions, d => d.ActionName == "Export");
        Assert.All(actions, d => Assert.True(
            d.EndpointMetadata.OfType<OpLogAttribute>().Any(), $"{d.DisplayName} 缺少 [OpLog]"));
        Assert.All(actions.Where(IsPost), d => Assert.True(
            d.EndpointMetadata.OfType<ValidateAntiForgeryTokenAttribute>().Any(), $"{d.DisplayName} 缺少 [ValidateAntiForgeryToken]"));
    }

    private static bool IsPost(ControllerActionDescriptor action)
        => action.EndpointMetadata.OfType<IActionHttpMethodProvider>().Any(p => p.HttpMethods.Contains("POST"));

    /// <summary>代码生成的全部权限码（不含测试中加载的探针页面）</summary>
    internal static HashSet<string> GeneratedPermCodes(CrmApp app)
        => AreaActions(app)
            .Where(d => d.ControllerTypeInfo.Assembly == typeof(Program).Assembly)
            .SelectMany(d => d.EndpointMetadata.OfType<HasPermissionAttribute>())
            .Select(p => p.Policy![HasPermissionAttribute.PolicyPrefix.Length..])
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>从测试程序所在目录往上找到放 Suanpan.sln 的仓库根目录</summary>
    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Suanpan.sln")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("找不到仓库根目录（Suanpan.sln）");
    }

    [GeneratedRegex(@"(?:asp-permission=""|HasAsync\("")(?<code>[^""]+)""")]
    private static partial Regex ViewPermCode();

    private static IEnumerable<ControllerActionDescriptor> AreaActions(CrmApp app)
        => app.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(d => d.RouteValues.TryGetValue("area", out var area) && !string.IsNullOrEmpty(area));
}
