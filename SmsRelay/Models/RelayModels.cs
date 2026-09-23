namespace SmsRelay.Models;

public enum RuleMatchMode { Exact, Prefix, Regex }
public enum RuleTarget { Sender, MessageBody }
public enum DeliveryStatus { Pending, Sending, Failed, Sent }

public sealed record SourceRule(Guid Id, string Pattern, RuleMatchMode Mode, RuleTarget Target = RuleTarget.Sender, bool IsEnabled = true)
{
    public override string ToString() => $"{(Target == RuleTarget.Sender ? "發送者" : "簡訊內容")}／{Mode}: {Pattern}";
}

public sealed class RelaySettings
{
    public string GotifyBaseUrl { get; set; } = string.Empty;
    public int Priority { get; set; } = 5;
    public List<SourceRule> Rules { get; set; } = [];
}

public sealed class QueueItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Sender { get; init; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public bool IsManualImport { get; init; }
    public string? DeduplicationKey { get; init; }
}

public sealed record SmsRecord(long Id, string Sender, string Body, DateTimeOffset ReceivedAt);
public sealed record SmsPage(IReadOnlyList<SmsRecord> Items, bool HasMore);
