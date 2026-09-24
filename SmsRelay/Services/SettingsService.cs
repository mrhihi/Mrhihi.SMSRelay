using System.Text.Json;
using System.Text.RegularExpressions;
using SmsRelay.Models;

namespace SmsRelay.Services;

public sealed class SettingsService : ISettingsService
{
    private const string LegacyTokenKey = "gotify-token";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path = Path.Combine(FileSystem.AppDataDirectory, "settings.json");

    public async Task<RelaySettings> GetAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var settings = File.Exists(_path)
                ? JsonSerializer.Deserialize<RelaySettings>(await File.ReadAllTextAsync(_path)) ?? new RelaySettings()
                : new RelaySettings();
            var migrated = await MigrateLegacyAsync(settings);
            migrated |= MigrateRuleGroups(settings);
            if (migrated) await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(settings));
            return settings;
        }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(RelaySettings settings, IReadOnlyDictionary<Guid, string>? tokenValues = null)
    {
        Validate(settings);
        await _gate.WaitAsync();
        try
        {
            await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(settings));
            if (tokenValues is not null)
                foreach (var pair in tokenValues.Where(x => !string.IsNullOrWhiteSpace(x.Value)))
                    await SecureStorage.SetAsync(TokenKey(pair.Key), pair.Value.Trim());
        }
        finally { _gate.Release(); }
    }

    public Task<string?> GetTokenAsync(Guid tokenId) => SecureStorage.GetAsync(TokenKey(tokenId));

    public async Task<IReadOnlyList<GotifyDestination>> GetDestinationsAsync()
        => (await GetAsync()).Servers.SelectMany(server => server.Tokens.Select(token => new GotifyDestination(server.Id, token.Id, server.Name, token.Name))).ToList();

    public async Task<IReadOnlyList<DeliveryTarget>> GetTargetsAsync(IEnumerable<Guid> tokenIds)
    {
        var requested = tokenIds.Distinct().ToHashSet();
        var settings = await GetAsync();
        var targets = new List<DeliveryTarget>();
        foreach (var server in settings.Servers)
        foreach (var token in server.Tokens.Where(x => requested.Contains(x.Id)))
        {
            var secret = await GetTokenAsync(token.Id);
            if (!string.IsNullOrWhiteSpace(secret))
                targets.Add(new DeliveryTarget(server.Id, token.Id, server.Name, token.Name, server.BaseUrl, secret, Math.Clamp(server.Priority, 0, 10)));
        }
        return targets;
    }

    public async Task<IReadOnlyList<DeliveryTarget>> GetAutomaticTargetsAsync(string sender, string body)
    {
        var settings = await GetAsync();
        var ids = settings.RuleGroups
            .Where(group => group.IsEnabled && MatchesGroup(group, sender, body))
            .SelectMany(group => group.TargetTokenIds)
            .Distinct();
        return await GetTargetsAsync(ids);
    }

    public async Task<IReadOnlyList<RuleMatchedMessage>> GetRuleMatchedMessagesAsync(IEnumerable<SmsRecord> messages, IEnumerable<Guid> ruleGroupIds)
    {
        var selectedIds = ruleGroupIds.Distinct().ToHashSet();
        if (selectedIds.Count == 0) return [];
        var settings = await GetAsync();
        var groups = settings.RuleGroups.Where(group => group.IsEnabled && selectedIds.Contains(group.Id)).ToList();
        var matched = new List<(SmsRecord Message, List<Guid> TokenIds)>();
        foreach (var message in messages)
        {
            var matchingGroups = groups.Where(group => MatchesGroup(group, message.Sender, message.Body)).ToList();
            if (matchingGroups.Count == 0) continue;
            matched.Add((message, matchingGroups.SelectMany(group => group.TargetTokenIds).Distinct().ToList()));
        }
        var targetByTokenId = (await GetTargetsAsync(matched.SelectMany(match => match.TokenIds))).ToDictionary(target => target.TokenId);
        var results = new List<RuleMatchedMessage>();
        foreach (var match in matched)
        {
            var targets = match.TokenIds.Where(targetByTokenId.ContainsKey).Select(tokenId => targetByTokenId[tokenId]).ToList();
            results.Add(new RuleMatchedMessage(match.Message, targets));
        }
        return results;
    }

    public static string NormalizePhone(string value) => new(value.Where(c => char.IsDigit(c) || c == '+').ToArray());

    private async Task<bool> MigrateLegacyAsync(RelaySettings settings)
    {
        if (settings.Servers.Count > 0 || string.IsNullOrWhiteSpace(settings.GotifyBaseUrl)) return false;
        var token = await SecureStorage.GetAsync(LegacyTokenKey);
        var legacyToken = new GotifyToken { Name = "預設 Token" };
        var server = new GotifyServer { Name = "預設 Gotify", BaseUrl = settings.GotifyBaseUrl, Priority = settings.Priority, Tokens = [legacyToken] };
        settings.Servers.Add(server);
        if (settings.Rules.Count > 0)
            settings.RuleGroups.Add(new RuleGroup { Name = "已遷移的規則", Rules = settings.Rules, TargetTokenIds = [legacyToken.Id] });
        settings.GotifyBaseUrl = string.Empty;
        settings.Rules = [];
        if (!string.IsNullOrWhiteSpace(token)) await SecureStorage.SetAsync(TokenKey(legacyToken.Id), token);
        return true;
    }

    private static bool MigrateRuleGroups(RelaySettings settings)
    {
        var changed = false;
        foreach (var group in settings.RuleGroups)
        {
            if (group.Clauses.Count > 0 || group.Rules.Count == 0) continue;
            var clause = new RuleClause();
            foreach (var rule in group.Rules)
            {
                if (clause.Rules.Count > 0 && rule.Join == RuleJoinOperator.Or)
                {
                    group.Clauses.Add(clause);
                    clause = new RuleClause();
                }
                clause.Rules.Add(rule with { Join = RuleJoinOperator.And });
            }
            if (clause.Rules.Count > 0) group.Clauses.Add(clause);
            group.Rules = [];
            changed = true;
        }
        return changed;
    }

    private static void Validate(RelaySettings settings)
    {
        foreach (var server in settings.Servers)
        {
            if (string.IsNullOrWhiteSpace(server.Name)) throw new InvalidOperationException("Gotify Server 名稱不可空白。");
            if (!Uri.TryCreate(server.BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException($"{server.Name} 的 URL 必須是有效的 HTTPS URL。");
            if (server.Tokens.Any(token => string.IsNullOrWhiteSpace(token.Name))) throw new InvalidOperationException("Token 名稱不可空白。");
        }
        var tokenIds = settings.Servers.SelectMany(x => x.Tokens).Select(x => x.Id).ToHashSet();
        foreach (var group in settings.RuleGroups)
        {
            if (string.IsNullOrWhiteSpace(group.Name)) throw new InvalidOperationException("規則群組名稱不可空白。");
            if (group.TargetTokenIds.Any(id => !tokenIds.Contains(id))) throw new InvalidOperationException($"群組「{group.Name}」包含不存在的 Token。");
            foreach (var rule in group.Clauses.SelectMany(x => x.Rules).Concat(group.Rules).Where(x => x.Mode == RuleMatchMode.Regex))
                _ = new Regex(rule.Pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
        }
    }

    private static bool MatchesGroup(RuleGroup group, string sender, string body)
    {
        return group.Clauses.Any(clause =>
        {
            var rules = clause.Rules.Where(x => x.IsEnabled).ToList();
            return rules.Count > 0 && rules.All(rule => Matches(rule, sender, body));
        });
    }

    private static bool Matches(SourceRule rule, string sender, string body)
    {
        var value = rule.Target == RuleTarget.MessageBody ? body : sender.Any(char.IsLetter) ? sender : NormalizePhone(sender);
        return rule.Mode switch
        {
            RuleMatchMode.Exact => string.Equals(value, rule.Pattern, StringComparison.OrdinalIgnoreCase),
            RuleMatchMode.Prefix => value.StartsWith(rule.Pattern, StringComparison.OrdinalIgnoreCase),
            RuleMatchMode.Regex => Regex.IsMatch(value, rule.Pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250)),
            _ => false
        };
    }

    private static string TokenKey(Guid tokenId) => $"gotify-token-{tokenId:N}";
}
