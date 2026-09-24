namespace SmsRelay.Models;

public enum RuleMatchMode { Exact, Prefix, Regex }
public enum RuleTarget { Sender, MessageBody }
public enum RuleJoinOperator { And, Or }
public enum DeliveryStatus { Pending, Sending, Failed, Sent }

public sealed record SourceRule(Guid Id, string Pattern, RuleMatchMode Mode, RuleTarget Target = RuleTarget.Sender, bool IsEnabled = true, RuleJoinOperator Join = RuleJoinOperator.And)
{
    public override string ToString() => $"{(Target == RuleTarget.Sender ? "發送者" : "簡訊內容")}／{Mode}: {Pattern}";
}

public sealed class RuleClause
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public List<SourceRule> Rules { get; set; } = [];
}

public sealed class RuleGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "新規則群組";
    public bool IsEnabled { get; set; } = true;
    // Kept for migration from the former linear AND/OR editor.
    public List<SourceRule> Rules { get; set; } = [];
    public List<RuleClause> Clauses { get; set; } = [];
    public List<Guid> TargetTokenIds { get; set; } = [];
}

public sealed class GotifyToken { public Guid Id { get; set; } = Guid.NewGuid(); public string Name { get; set; } = string.Empty; }
public sealed class GotifyServer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public int Priority { get; set; } = 5;
    public List<GotifyToken> Tokens { get; set; } = [];
}

public sealed class RelaySettings
{
    // Legacy fields are retained solely to migrate existing installations.
    public string GotifyBaseUrl { get; set; } = string.Empty;
    public int Priority { get; set; } = 5;
    public List<SourceRule> Rules { get; set; } = [];
    public List<GotifyServer> Servers { get; set; } = [];
    public List<RuleGroup> RuleGroups { get; set; } = [];
}

public sealed record GotifyDestination(Guid ServerId, Guid TokenId, string ServerName, string TokenName);
public sealed record DeliveryTarget(Guid ServerId, Guid TokenId, string ServerName, string TokenName, string BaseUrl, string Token, int Priority);

public sealed class QueueItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Sender { get; init; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;
    public Guid MessageGroupId { get; set; } = Guid.NewGuid();
    public DeliveryTarget? Target { get; set; }
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public bool IsManualImport { get; init; }
    public string? DeduplicationKey { get; init; }
}

public sealed record SmsRecord(long Id, string Sender, string Body, DateTimeOffset ReceivedAt);
public sealed record SmsPage(IReadOnlyList<SmsRecord> Items, bool HasMore);
