using Microsoft.AspNetCore.Mvc.Rendering;
using Suanpan.Core.Collect.Rules;
using Suanpan.Services.Collect.Tasks;

namespace Suanpan.Crm.Models.Collect;

/// <summary>
/// 解析器下拉框（采集设计 §8.1）：按适配器分组，文字带标识和目标数据类型；
/// 当前值已不在目录中时另加一项，免得编辑时被悄悄换掉。采集任务和指标数据源映射的表单共用
/// </summary>
public static class ParserSelectOptions
{
    public static List<SelectListItem> Build(IParserCatalog parsers, string? currentKey)
    {
        var groups = new Dictionary<string, SelectListGroup>(StringComparer.Ordinal);
        var options = parsers.All
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => new SelectListItem($"{p.Name}（{p.Key}，{CollectDisplayNames.Get(p.TargetType)}）", p.Key)
            {
                Group = groups.TryGetValue(p.AdapterKey, out var group) ? group : groups[p.AdapterKey] = new SelectListGroup { Name = p.AdapterKey },
            })
            .ToList();
        if (!string.IsNullOrEmpty(currentKey) && parsers.Find(currentKey) is null)
        {
            options.Insert(0, new SelectListItem($"{currentKey}（目录中已没有）", currentKey));
        }

        return options;
    }
}
