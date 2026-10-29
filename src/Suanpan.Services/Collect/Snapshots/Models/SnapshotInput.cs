namespace Suanpan.Services.Collect.Snapshots.Models;

/// <summary>
/// 要保存为快照的一个响应（采集设计 §5.5），由 Collector 从适配器的响应转换而来。
/// Url 不含查询串；RequestKey 是判断"未变化"用的请求标识，不含凭据（BR-021）
/// </summary>
public sealed record SnapshotInput(string Url, string RequestKey, int HttpStatus, string? ContentType, byte[] Body, DateTime FetchedAt);
