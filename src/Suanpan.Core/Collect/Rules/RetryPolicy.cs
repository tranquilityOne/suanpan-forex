using System.Globalization;

namespace Suanpan.Core.Collect.Rules;

/// <summary>
/// 采集请求失败后的重试（采集设计 §3.4，BR-009）。重试间隔取系统配置 collect:retry_delay_seconds（逗号分隔的秒数），
/// 间隔的个数就是最多重试的次数；默认 30 秒、2 分钟、5 分钟，共重试 3 次。
/// </summary>
public static class RetryPolicy
{
    /// <summary>系统配置 collect:retry_delay_seconds 的默认值</summary>
    public const string DefaultDelaySeconds = "30,120,300";

    /// <summary>解析重试间隔，如 "30,120,300"；空串表示不重试；格式不对（非整数、负数）时返回 null，调用方改用默认值</summary>
    public static IReadOnlyList<TimeSpan>? ParseDelays(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var delays = new List<TimeSpan>();
        foreach (var part in value.Split(','))
        {
            if (!int.TryParse(part.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
            {
                return null;
            }

            delays.Add(TimeSpan.FromSeconds(seconds));
        }

        return delays;
    }

    /// <summary>
    /// 可以重试的失败：超时或网络错误（没有状态码）、HTTP 5xx、429。其他 4xx 多半是地址、参数或凭据错误，重试也不会成功；
    /// 解析失败不经过这里，一律不重试
    /// </summary>
    public static bool IsRetryable(int? httpStatus) => httpStatus is null or >= 500 or 429;

    /// <summary>第 attempt 次请求（从 1 开始）失败后，等多久再发下一次；重试次数用完时返回 null</summary>
    public static TimeSpan? DelayAfter(int attempt, IReadOnlyList<TimeSpan> delays)
        => attempt >= 1 && attempt <= delays.Count ? delays[attempt - 1] : null;
}
