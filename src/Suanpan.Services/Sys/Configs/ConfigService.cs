using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Suanpan.Core.Sys.Entities;
using Suanpan.Core.Sys.Enums;
using Suanpan.Core.Sys.Rules;
using Suanpan.Data;
using Suanpan.Services.Sys.Configs.Models;
using Suanpan.Shared.Mvc;

namespace Suanpan.Services.Sys.Configs;

/// <param name="definitions">代码中定义的配置项；不传时用 <see cref="SysConfigKeys.Definitions"/>（测试中可以替换）</param>
public sealed class ConfigService(
    SuanpanDbContext db,
    IMemoryCache cache,
    ILogger<ConfigService> logger,
    IReadOnlyList<ConfigDefinition>? definitions = null) : IConfigService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly IReadOnlyList<ConfigDefinition> _definitions = definitions ?? SysConfigKeys.Definitions;

    /// <summary>缓存键（sys 设计 §6.5）</summary>
    public static string CacheKey(string key) => $"config:{key}";

    /// <summary>缓存的值；Value 为 null 表示库里没有，同样缓存，避免反复查库</summary>
    private sealed record CachedValue(string? Value);

    public async Task<string?> GetStringAsync(string key, string? defaultValue, CancellationToken ct = default)
        => await GetRawAsync(key, ct) ?? defaultValue;

    public async Task<int> GetIntAsync(string key, int defaultValue, CancellationToken ct = default)
        => Parse(key, await GetRawAsync(key, ct), ConfigValueType.Int, defaultValue,
            v => int.Parse(v, CultureInfo.InvariantCulture));

    public async Task<decimal> GetDecimalAsync(string key, decimal defaultValue, CancellationToken ct = default)
        => Parse(key, await GetRawAsync(key, ct), ConfigValueType.Decimal, defaultValue,
            v => decimal.Parse(v, CultureInfo.InvariantCulture));

    public async Task<bool> GetBoolAsync(string key, bool defaultValue, CancellationToken ct = default)
        => Parse(key, await GetRawAsync(key, ct), ConfigValueType.Bool, defaultValue, v => v == "true");

    public async Task<IReadOnlyList<ConfigRow>> GetListAsync(CancellationToken ct = default)
    {
        var stored = await db.SysConfigs.AsNoTracking().ToDictionaryAsync(c => c.ConfigKey, ct);

        var rows = _definitions.Select(d => new ConfigRow(
            d.Key, d.Group, d.ValueType, d.Description, d.DefaultValue,
            stored.GetValueOrDefault(d.Key)?.ConfigValue, IsDefined: true)).ToList();

        var definedKeys = _definitions.Select(d => d.Key).ToHashSet(StringComparer.Ordinal);
        rows.AddRange(stored.Values.Where(c => !definedKeys.Contains(c.ConfigKey)).Select(c => new ConfigRow(
            c.ConfigKey, c.ConfigGroup, c.ValueType, c.Description, DefaultValue: null, c.ConfigValue, IsDefined: false)));

        return rows.OrderBy(r => r.Group, StringComparer.Ordinal).ThenBy(r => r.Key, StringComparer.Ordinal).ToList();
    }

    public async Task<ManageResult> SaveAsync(string key, string value, CancellationToken ct = default)
    {
        var definition = _definitions.FirstOrDefault(d => d.Key == key);
        var row = await db.SysConfigs.FirstOrDefaultAsync(c => c.ConfigKey == key, ct);
        var valueType = definition?.ValueType ?? row?.ValueType;
        if (valueType is null)
        {
            return ManageResult.Fail($"未定义的配置项：{key}");
        }

        if (!ConfigValueRule.TryNormalize(valueType.Value, value, out var normalized))
        {
            return ManageResult.Fail("配置值不符合该项的类型要求");
        }

        if (row is null)
        {
            db.SysConfigs.Add(new SysConfig
            {
                ConfigKey = key,
                ConfigValue = normalized,
                ValueType = valueType.Value,
                ConfigGroup = definition!.Group,
                Description = definition.Description,
            });
        }
        else
        {
            row.ConfigValue = normalized;
            row.ValueType = valueType.Value;
        }

        await db.SaveChangesAsync(ct);
        cache.Remove(CacheKey(key));
        return ManageResult.Ok;
    }

    public async Task<ManageResult> ResetAsync(string key, CancellationToken ct = default)
    {
        await db.SysConfigs.Where(c => c.ConfigKey == key).ExecuteDeleteAsync(ct);
        cache.Remove(CacheKey(key));
        return ManageResult.Ok;
    }

    private async Task<string?> GetRawAsync(string key, CancellationToken ct)
    {
        var cached = await cache.GetOrCreateAsync(CacheKey(key), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            var value = await db.SysConfigs.AsNoTracking()
                .Where(c => c.ConfigKey == key)
                .Select(c => c.ConfigValue)
                .FirstOrDefaultAsync(ct);
            return new CachedValue(value);
        });

        return cached?.Value;
    }

    private T Parse<T>(string key, string? raw, ConfigValueType valueType, T defaultValue, Func<string, T> convert)
    {
        if (raw is null)
        {
            return defaultValue;
        }

        if (ConfigValueRule.TryNormalize(valueType, raw, out var normalized))
        {
            return convert(normalized);
        }

        logger.LogWarning("配置 {ConfigKey} 的值无法解析为 {ValueType}，使用默认值", key, valueType);
        return defaultValue;
    }
}
