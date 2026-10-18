using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Suanpan.Core.Sys.Entities;
using Suanpan.Services.Sys.Logging;
using Suanpan.Services.Sys.Logging.Models;
using Suanpan.Services.Sys.Rbac;
using Suanpan.Services.Tests.Fixtures;
using Suanpan.Shared.Auth;
using Suanpan.Shared.Mvc;
using Suanpan.Shared.Paging;
using Xunit;

namespace Suanpan.Services.Tests.Sys.Logging;

public class OpLogActionFilterTests
{
    private readonly RecordingOpLogService _logs = new();

    [Fact]
    public async Task ActionWithoutOpLog_WritesNothing()
    {
        var context = CreateContext(withOpLog: false);

        await RunAsync(context);

        Assert.Empty(_logs.Written);
    }

    [Fact]
    public async Task SuccessfulAction_RecordsRequestParamsOnly_WithMaskingRolesPermCodeAndReason()
    {
        var context = CreateContext(arguments: new Dictionary<string, object?>
        {
            ["userId"] = 7L,
            ["newPassword"] = "plain-secret",
            ["input"] = new { Reason = "离职交接" },
        });

        await RunAsync(context);

        var log = Assert.Single(_logs.Written);
        Assert.Equal((42L, "zhangsan", "editor,viewer"), (log.UserId, log.Username, log.Roles));
        Assert.Equal(("系统管理", "停用账号", "sys:user:set_status"), (log.Module, log.Action, log.PermCode));
        Assert.Equal(("POST", "/Sys/User/SetStatus", "203.0.113.5"), (log.HttpMethod, log.RequestUrl, log.Ip));
        Assert.Equal("离职交接", log.Reason);
        Assert.True(log.IsSuccess);
        Assert.Contains("\"userId\":7", log.RequestParams);
        Assert.DoesNotContain("plain-secret", log.RequestParams);
    }

    [Fact]
    public async Task InvalidModelState_IsRecordedAsFailure()
    {
        var context = CreateContext();
        context.ModelState.AddModelError("RealName", "姓名不能为空");

        await RunAsync(context);

        Assert.False(Assert.Single(_logs.Written).IsSuccess);
    }

    [Fact]
    public async Task TempDataError_IsRecordedAsFailure()
    {
        var context = CreateContext();
        ((Controller)context.Controller).TempData["Error"] = "不能停用自己";

        await RunAsync(context);

        Assert.False(Assert.Single(_logs.Written).IsSuccess);
    }

    [Fact]
    public async Task UnhandledException_IsRecordedAsFailure()
    {
        var context = CreateContext();

        await RunAsync(context, new InvalidOperationException("boom"));

        var log = Assert.Single(_logs.Written);
        Assert.False(log.IsSuccess);
        Assert.Equal("boom", log.ErrorMsg);
    }

    [Fact]
    public async Task WriteFailure_DoesNotAffectTheRequest()
    {
        _logs.ThrowOnAdd = true;

        var exception = await Record.ExceptionAsync(() => RunAsync(CreateContext()));

        Assert.Null(exception);
    }

    private async Task RunAsync(ActionExecutingContext context, Exception? actionException = null)
    {
        var filter = new OpLogActionFilter(
            _logs, new StubPermissionService(["editor", "viewer"]), new StubCurrentUser(42, "zhangsan"),
            NullLogger<OpLogActionFilter>.Instance);
        var executed = new ActionExecutedContext(context, [], context.Controller) { Exception = actionException };

        await filter.OnActionExecutionAsync(context, () => Task.FromResult(executed));
    }

    private static ActionExecutingContext CreateContext(bool withOpLog = true, Dictionary<string, object?>? arguments = null)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = "POST";
        http.Request.Path = "/Sys/User/SetStatus";
        http.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.5");

        var descriptor = new ControllerActionDescriptor
        {
            EndpointMetadata = withOpLog
                ? [new OpLogAttribute("系统管理", "停用账号"), new HasPermissionAttribute("sys:user:set_status")]
                : [new HasPermissionAttribute("sys:user:index")],
        };
        var actionContext = new ActionContext(http, new RouteData(), descriptor);
        var controller = new TestController
        {
            ControllerContext = new ControllerContext(actionContext),
            TempData = new TempDataDictionary(http, new EmptyTempDataProvider()),
        };

        return new ActionExecutingContext(actionContext, [], arguments ?? new Dictionary<string, object?>(), controller);
    }

    private sealed class TestController : Controller;

    private sealed class EmptyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }

    private sealed class RecordingOpLogService : IOpLogService
    {
        public List<SysOpLog> Written { get; } = [];

        public bool ThrowOnAdd { get; set; }

        public Task AddAsync(SysOpLog log, CancellationToken ct = default)
        {
            if (ThrowOnAdd)
            {
                throw new InvalidOperationException("数据库不可用");
            }

            Written.Add(log);
            return Task.CompletedTask;
        }

        public Task<PagedResult<OpLogRow>> QueryAsync(OpLogQuery query, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<int> ExportAsync(OpLogQuery query, Stream output, TimeZoneInfo timeZone, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class StubPermissionService(IReadOnlyList<string> roleCodes) : IPermissionService
    {
        public Task<PermissionSnapshot> GetSnapshotAsync(long userId, CancellationToken ct = default)
            => Task.FromResult(new PermissionSnapshot(false, roleCodes, new HashSet<string>()));

        public void Invalidate(long userId)
        {
        }

        public Task InvalidateRoleHoldersAsync(long roleId, CancellationToken ct = default) => Task.CompletedTask;

        public void InvalidateAll()
        {
        }
    }
}
