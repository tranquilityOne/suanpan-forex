namespace Suanpan.Crm.Mvc;

/// <summary>
/// 操作者时区：页面上的时间、导出文件中的时间都按它显示（前端约定 §7.2）。
/// 第一阶段统一为 Asia/Shanghai，按账号设置时区是后续扩展（sys 设计 §9.3）。
/// </summary>
public interface IOperatorTimeZone
{
    TimeZoneInfo TimeZone { get; }
}

public sealed class DefaultOperatorTimeZone : IOperatorTimeZone
{
    public TimeZoneInfo TimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
}
