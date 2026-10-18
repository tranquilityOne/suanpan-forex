namespace Suanpan.Crm.Tests.Fixtures;

public static class HttpClientExtensions
{
    /// <summary>带登录 Cookie 取页面 HTML，非 2xx 时抛出异常</summary>
    public static async Task<string> GetStringAsync(this HttpClient client, string path, string cookie, CancellationToken ct)
    {
        var response = await client.SendAsync(Requests.Get(path, cookie), ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
