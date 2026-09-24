using System.Net.Http.Json;
using SmsRelay.Models;

namespace SmsRelay.Services;

public sealed class GotifyClient : IGotifyClient
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public async Task SendAsync(QueueItem item, CancellationToken cancellationToken)
    {
        if (item.Target is null) throw new InvalidOperationException("此傳送紀錄沒有指定 Gotify 目的地。");
        var target = item.Target;
        using var response = await _http.PostAsJsonAsync(GetEndpoint(target), new
        {
            title = $"SMS：{item.Sender}",
            message = $"時間：{item.ReceivedAt.LocalDateTime:yyyy-MM-dd HH:mm:ss}\n來源：{item.Sender}\n\n{item.Body}",
            priority = target.Priority
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task TestAsync(DeliveryTarget target, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(GetEndpoint(target), new { title = "SMS Relay", message = $"Gotify 連線測試成功（{target.ServerName}／{target.TokenName}）。", priority = target.Priority }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static string GetEndpoint(DeliveryTarget target)
    {
        if (!Uri.TryCreate(target.BaseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(target.Token))
            throw new InvalidOperationException("Gotify 目的地設定無效。");
        var endpoint = new Uri(baseUri, baseUri.AbsolutePath.TrimEnd('/') + "/message");
        return new UriBuilder(endpoint) { Query = "token=" + Uri.EscapeDataString(target.Token) }.Uri.ToString();
    }
}
