using Microsoft.AspNetCore.Mvc.Rendering;
using Suanpan.Core.Collect.Rules;

namespace Suanpan.Crm.Models.Collect;

/// <summary>数据采集模块枚举的下拉框选项：值为枚举名，文字为中文显示名（CollectDisplayNames）</summary>
public static class CollectEnumOptions
{
    public static List<SelectListItem> Of<TEnum>()
        where TEnum : struct, Enum
        => Enum.GetValues<TEnum>().Select(value => new SelectListItem(CollectDisplayNames.Get(value), value.ToString())).ToList();
}
