using System.Net.Http.Json;
using SmsRelay.Models;

namespace SmsRelay.Services;

public sealed class GotifyClient(ISettingsService settings) : IGotifyClient
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public async Task SendAsync(QueueItem item, CancellationToken cancellationToken)
    {
        var (url, priority) = await GetEndpointAsync();
        var payload = new { title = $"SMS：{item.Sender}", message = $"時間：{item.ReceivedAt.LocalDateTime:yyyy-MM-dd HH:mm:ss}\n來源：{item.Sender}\n\n{item.Body}", priority };
        using var response = await _http.PostAsJsonAsync(url, payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task TestAsync(CancellationToken cancellationToken)
    {
        var (url, priority) = await GetEndpointAsync();
        using var response = await _http.PostAsJsonAsync(url, new { title = "SMS Relay", message = "Gotify 連線測試成功。", priority }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<(string Url, int Priority)> GetEndpointAsync()
    {
        var value = await settings.GetAsync();
        var token = await settings.GetTokenAsync();
        if (!Uri.TryCreate(value.GotifyBaseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("請先設定有效的 HTTPS Gotify URL 與 application token。");
        var endpoint = new Uri(baseUri, baseUri.AbsolutePath.TrimEnd('/') + "/message");
        var builder = new UriBuilder(endpoint) { Query = "token=" + Uri.EscapeDataString(token) };
        return (builder.Uri.ToString(), Math.Clamp(value.Priority, 0, 10));
    }
}
