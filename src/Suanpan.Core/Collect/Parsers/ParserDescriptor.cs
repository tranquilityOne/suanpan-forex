using Suanpan.Core.Collect.Enums;

namespace Suanpan.Core.Collect.Parsers;

/// <summary>
/// 解析器目录中的一项（采集设计 §5.2）。Key 的写法为 {经济体}.{机构}.{内容}，全部小写，如 us.bls.schedule；
/// AdapterKey 为负责请求的适配器，取 Key 的前两段，如 us.bls
/// </summary>
public sealed record ParserDescriptor(string Key, string Name, CollectTargetType TargetType)
{
    public string AdapterKey => Key[..Key.LastIndexOf('.')];
}
