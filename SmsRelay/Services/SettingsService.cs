using System.Text.RegularExpressions;
using System.Text.Json;
using SmsRelay.Models;

namespace SmsRelay.Services;

public sealed class SettingsService : ISettingsService
{
    private const string TokenKey = "gotify-token";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path = Path.Combine(FileSystem.AppDataDirectory, "settings.json");

    public async Task<RelaySettings> GetAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (!File.Exists(_path)) return new RelaySettings();
            return JsonSerializer.Deserialize<RelaySettings>(await File.ReadAllTextAsync(_path)) ?? new RelaySettings();
        }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(RelaySettings settings, string? gotifyToken)
    {
        if (!string.IsNullOrWhiteSpace(settings.GotifyBaseUrl) &&
            (!Uri.TryCreate(settings.GotifyBaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("Gotify URL 必須是有效的 HTTPS URL。");

        foreach (var rule in settings.Rules.Where(x => x.Mode == RuleMatchMode.Regex))
            _ = new Regex(rule.Pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));

        await _gate.WaitAsync();
        try
        {
            await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(settings));
            if (gotifyToken is not null) await SecureStorage.SetAsync(TokenKey, gotifyToken.Trim());
        }
        finally { _gate.Release(); }
    }

    public Task<string?> GetTokenAsync() => SecureStorage.GetAsync(TokenKey);

    public async Task<bool> IsSmsAllowedAsync(string sender, string body)
    {
        var rules = (await GetAsync()).Rules.Where(x => x.IsEnabled).ToList();
        var normalized = NormalizePhone(sender);
        return rules.Any(rule => Matches(rule, sender, normalized, body));
    }

    public static string NormalizePhone(string value) => new(value.Where(c => char.IsDigit(c) || c == '+').ToArray());

    private static bool Matches(SourceRule rule, string sender, string normalized, string body)
    {
        var value = rule.Target == RuleTarget.MessageBody ? body : sender.Any(char.IsLetter) ? sender : normalized;
        return rule.Mode switch
        {
            RuleMatchMode.Exact => string.Equals(value, rule.Pattern, StringComparison.OrdinalIgnoreCase),
            RuleMatchMode.Prefix => value.StartsWith(rule.Pattern, StringComparison.OrdinalIgnoreCase),
            RuleMatchMode.Regex => Regex.IsMatch(value, rule.Pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250)),
            _ => false
        };
    }
}
