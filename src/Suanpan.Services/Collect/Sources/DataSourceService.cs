using System.Text;
using Microsoft.EntityFrameworkCore;
using Suanpan.Core.Collect.Entities;
using Suanpan.Core.Collect.Enums;
using Suanpan.Core.Collect.Rules;
using Suanpan.Core.Reference.Rules;
using Suanpan.Data;
using Suanpan.Infrastructure.Http;
using Suanpan.Infrastructure.Redis;
using Suanpan.Services.Collect.Sources.Models;
using Suanpan.Services.Common.References;
using Suanpan.Services.Sys.Security;
using Suanpan.Shared.Mvc;

namespace Suanpan.Services.Collect.Sources;

public sealed class DataSourceService(
    SuanpanDbContext db,
    ISecretProtector secretProtector,
    IHttpClientFactory httpClientFactory,
    IRateLimiter rateLimiter,
    TimeProvider timeProvider) : IDataSourceService
{
    /// <summary>连通性测试的超时（PRD FR-001 异常流程"连通性失败"）</summary>
    public static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    /// <summary>连通性测试返回的响应片段长度</summary>
    public const int SnippetLength = 500;

    public async Task<IReadOnlyList<DataSourceRow>> GetListAsync(CancellationToken ct = default)
        => await db.DataSources.AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new DataSourceRow(
                s.Id, s.Name, s.Agency, s.EconomyCode, s.Type, s.RateLimit, s.RateLimitUnit,
                s.ComplianceClass, s.RobotsResult, s.CredentialRequired, s.Credential != null, s.IsEnabled,
                db.CollectTasks.Count(t => t.SourceId == s.Id)))
            .ToListAsync(ct);

    public Task<DataSource?> GetAsync(long id, CancellationToken ct = default)
        => db.DataSources.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<DataSource>> GetOptionsAsync(CancellationToken ct = default)
        => await db.DataSources.AsNoTracking().Where(s => s.IsEnabled).OrderBy(s => s.Name).ToListAsync(ct);

    public async Task<ManageResult> CreateAsync(DataSourceInput input, CancellationToken ct = default)
    {
        var source = new DataSource { IsEnabled = false };
        if (await ValidateReferencesAsync(source, input, ct) is { } invalid)
        {
            return invalid;
        }

        Apply(source, input);
        if (DataSourceRule.Validate(source) is { } error)
        {
            return ManageResult.Fail(error.Message, error.Field);
        }

        db.DataSources.Add(source);
        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    public async Task<ManageResult> UpdateAsync(long id, DataSourceInput input, CancellationToken ct = default)
    {
        if (await db.DataSources.FirstOrDefaultAsync(s => s.Id == id, ct) is not { } source)
        {
            return ManageResult.Fail("数据源不存在");
        }

        if (await ValidateReferencesAsync(source, input, ct) is { } invalid)
        {
            return invalid;
        }

        // 校验失败时不保存，留在上下文里的改动随请求结束丢弃（操作日志用单独的 INSERT 写入，不会带上它们）
        Apply(source, input);
        if (DataSourceRule.Validate(source) is { } error)
        {
            return ManageResult.Fail(error.Message, error.Field);
        }

        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    public async Task<ManageResult> SetEnabledAsync(long id, bool enabled, CancellationToken ct = default)
    {
        if (await db.DataSources.FirstOrDefaultAsync(s => s.Id == id, ct) is not { } source)
        {
            return ManageResult.Fail("数据源不存在");
        }

        if (enabled && DataSourceRule.ValidateCanEnable(source) is { } error)
        {
            return ManageResult.Fail(error.Message);
        }

        source.IsEnabled = enabled;
        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    public async Task<ManageResult> DeleteAsync(long id, CancellationToken ct = default)
    {
        if (await db.DataSources.FirstOrDefaultAsync(s => s.Id == id, ct) is not { } source)
        {
            return ManageResult.Fail("数据源不存在");
        }

        // 引用方（BR-006、ERR-SRC-003，采集设计 §3.1，日历设计 §7）：新闻加入后在这里补充
        var tasks = await db.CollectTasks.AsNoTracking()
            .Where(t => t.SourceId == source.Id)
            .OrderBy(t => t.Name)
            .Select(t => t.Name)
            .ToListAsync(ct);
        var indicators = await db.IndicatorSources.AsNoTracking()
            .Where(m => m.SourceId == source.Id)
            .OrderBy(m => m.IndicatorCode)
            .Select(m => m.IndicatorCode)
            .ToListAsync(ct);
        // 日历事件：排期或公布值来自它、有它的数值或锁定冲突；日程中的发布标识：未匹配条目、已忽略的发布
        var eventIds = db.CalendarEvents.Where(e => e.ScheduleSourceId == source.Id || e.ActualSourceId == source.Id).Select(e => e.Id)
            .Union(db.CalendarEventValues.Where(v => v.SourceId == source.Id).Select(v => v.EventId))
            .Union(db.LockConflicts.Where(c => c.SourceId == source.Id).Select(c => c.EventId));
        var events = await db.CalendarEvents.AsNoTracking()
            .Where(e => eventIds.Contains(e.Id))
            .OrderBy(e => e.IndicatorCode).ThenBy(e => e.ReferencePeriod)
            .Select(e => e.IndicatorCode + " " + e.ReferencePeriod)
            .ToListAsync(ct);
        var releases = await db.UnmatchedEntries.Where(u => u.SourceId == source.Id).Select(u => u.ReleaseKey)
            .Union(db.IgnoredReleases.Where(i => i.SourceId == source.Id).Select(i => i.ReleaseKey))
            .OrderBy(key => key)
            .ToListAsync(ct);
        if (ReferenceUsage.DescribeOrNull(("采集任务", tasks), ("指标映射", indicators), ("日历事件", events), ("日程发布标识", releases)) is { } usage)
        {
            return ManageResult.Fail(usage);
        }

        db.DataSources.Remove(source);
        await db.SaveChangesAsync(ct);
        return ManageResult.Ok;
    }

    public async Task<DataSourceTestResult> TestAsync(long id, CancellationToken ct = default)
    {
        if (await GetAsync(id, ct) is not { } source)
        {
            return DataSourceTestResult.NotSent("数据源不存在");
        }

        if (source.RobotsResult == RobotsResult.Disallowed)
        {
            return DataSourceTestResult.NotSent("robots.txt 不允许采集，不发请求");
        }

        if (!await rateLimiter.TryAcquireAsync(DataSourceRateLimit.Key(source.Id), source.RateLimit, DataSourceRule.RateLimitWindow(source.RateLimitUnit)))
        {
            return DataSourceTestResult.NotSent("已达到这个数据源的请求频率上限，请稍后再试");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TestTimeout);
        var client = httpClientFactory.CreateClient(CollectHttp.ClientName);
        var started = timeProvider.GetTimestamp();
        try
        {
            using var response = await client.GetAsync(source.BaseUrl, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var snippet = await ReadSnippetAsync(response.Content, timeout.Token);
            var status = (int)response.StatusCode;
            return new DataSourceTestResult(
                response.IsSuccessStatusCode, status, ElapsedMs(started), snippet,
                response.IsSuccessStatusCode ? null : $"HTTP {status} {response.ReasonPhrase}".TrimEnd());
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new DataSourceTestResult(false, null, ElapsedMs(started), null, $"请求超时（{TestTimeout.TotalSeconds:0} 秒）");
        }
        catch (HttpRequestException ex)
        {
            return new DataSourceTestResult(false, null, ElapsedMs(started), null, ex.Message);
        }
    }

    /// <summary>名称不重复；经济体存在，新选用的经济体必须启用（BR-028）。按输入判断，不改实体</summary>
    private async Task<ManageResult?> ValidateReferencesAsync(DataSource source, DataSourceInput input, CancellationToken ct)
    {
        var name = input.Name.Trim();
        if (await db.DataSources.AnyAsync(s => s.Name == name && s.Id != source.Id, ct))
        {
            return ManageResult.Fail($"数据源名称\"{name}\"已存在", nameof(DataSourceInput.Name));
        }

        var economyCode = EconomyCodeOf(input);
        if (economyCode is null || economyCode == source.EconomyCode)
        {
            return null;
        }

        var economy = await db.Economies.AsNoTracking().FirstOrDefaultAsync(e => e.Code == economyCode, ct);
        if (economy is null)
        {
            return ManageResult.Fail("经济体不存在", nameof(DataSourceInput.EconomyCode));
        }

        return economy.IsEnabled ? null : ManageResult.Fail($"经济体 {economyCode} 已停用，不能选用", nameof(DataSourceInput.EconomyCode));
    }

    /// <summary>把输入写到实体上；凭据为空时不改变，勾选清除时清空，否则加密保存（BR-004）</summary>
    private void Apply(DataSource source, DataSourceInput input)
    {
        source.Name = input.Name.Trim();
        source.Agency = input.Agency.Trim();
        source.EconomyCode = EconomyCodeOf(input);
        source.Type = input.Type;
        source.BaseUrl = input.BaseUrl.Trim();
        source.RateLimit = input.RateLimit;
        source.RateLimitUnit = input.RateLimitUnit;
        source.TimeoutSeconds = input.TimeoutSeconds;
        source.CredentialRequired = input.CredentialRequired;
        source.ComplianceClass = input.ComplianceClass;
        source.TermsUrl = input.TermsUrl.Trim();
        source.RobotsResult = input.RobotsResult;
        source.Remark = string.IsNullOrWhiteSpace(input.Remark) ? null : input.Remark.Trim();

        if (input.ClearCredential)
        {
            source.Credential = null;
        }
        else if (!string.IsNullOrWhiteSpace(input.Credential))
        {
            source.Credential = secretProtector.Encrypt(input.Credential.Trim());
        }
    }

    private static string? EconomyCodeOf(DataSourceInput input)
        => string.IsNullOrWhiteSpace(input.EconomyCode) ? null : ReferenceCodeRule.Normalize(input.EconomyCode);

    private long ElapsedMs(long started) => (long)timeProvider.GetElapsedTime(started).TotalMilliseconds;

    /// <summary>读响应开头的 <see cref="SnippetLength"/> 个字符，按响应声明的字符集解码（默认 UTF-8），不读完整个响应</summary>
    private static async Task<string> ReadSnippetAsync(HttpContent content, CancellationToken ct)
    {
        var encoding = Encoding.UTF8;
        if (content.Headers.ContentType?.CharSet is { Length: > 0 } charset)
        {
            try
            {
                encoding = Encoding.GetEncoding(charset.Trim('"'));
            }
            catch (ArgumentException)
            {
                // 不认识的字符集按 UTF-8 解码
            }
        }

        await using var stream = await content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, encoding);
        var buffer = new char[SnippetLength];
        var read = await reader.ReadBlockAsync(buffer.AsMemory(), ct);
        return new string(buffer, 0, read);
    }
}
