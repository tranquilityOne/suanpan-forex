using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Suanpan.Services.Sys.Logging;

/// <summary>
/// 操作日志的请求参数序列化与脱敏（sys 设计 §7.1，BR-147）：参数名包含敏感词（不区分大小写）的字段，值替换为 ***；
/// 递归处理嵌套对象和数组；无法序列化的参数记占位符；整体截断到 <see cref="MaxLength"/> 个字符。
/// </summary>
public static class SensitiveDataMasker
{
    public const int MaxLength = 2000;

    public const string Masked = "***";

    /// <summary>
    /// 敏感词。不要加入单独的 key、code，否则会误伤 config_key、perm_code 这类正常字段。
    /// </summary>
    public static readonly IReadOnlyList<string> SensitiveWords =
    [
        "password", "pwd", "secret", "token", "totp", "apikey", "api_key",
        "privatekey", "private_key", "credential", "encryptionkey",
    ];

    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>把 action 的参数序列化为脱敏后的 JSON；没有参数时返回 null</summary>
    public static string? SerializeArguments(IDictionary<string, object?> arguments)
    {
        var root = new JsonObject();
        foreach (var (name, value) in arguments)
        {
            if (value is CancellationToken)
            {
                continue;
            }

            if (IsSensitive(name))
            {
                root[name] = Masked;
                continue;
            }

            try
            {
                root[name] = value is null ? null : JsonSerializer.SerializeToNode(value, value.GetType(), SerializeOptions);
            }
            catch (Exception)
            {
                // 文件流等无法序列化的参数：记占位符，不能让日志影响请求
                root[name] = "<无法序列化>";
            }
        }

        if (root.Count == 0)
        {
            return null;
        }

        Mask(root);
        var json = root.ToJsonString(SerializeOptions);
        return json.Length <= MaxLength ? json : json[..MaxLength];
    }

    public static bool IsSensitive(string name)
        => SensitiveWords.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase));

    private static void Mask(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    if (IsSensitive(key))
                    {
                        obj[key] = Masked;
                    }
                    else
                    {
                        Mask(obj[key]);
                    }
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    Mask(item);
                }

                break;
        }
    }
}
