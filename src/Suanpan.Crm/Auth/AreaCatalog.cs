using Suanpan.Services.Sys.Rbac.Models;

namespace Suanpan.Crm.Auth;

/// <summary>
/// 后台栏目清单（sys 设计 §6.2）：权限树重建时每个 Area 生成一个目录节点。
/// Area 名与模块目录名一致（后端约定 §1.1）；新增栏目时同时补充这里和前端约定 §3.2。
/// </summary>
public static class AreaCatalog
{
    public static readonly IReadOnlyList<AreaDirectory> All =
    [
        new("Calendar", "财经日历", "calendar-event", 10),
        new("Flash", "快讯", "bolt", 20),
        new("Collect", "数据采集", "cloud-download", 30),
        new("Reference", "基础数据", "database", 40),
        new("Market", "市场数据", "chart-line", 50),
        new("News", "新闻", "news", 60),
        new("Publish", "前台发布", "world-upload", 70),
        new("Ops", "运维", "activity", 80),
        new("Sys", "系统管理", "settings", 90),
    ];
}
