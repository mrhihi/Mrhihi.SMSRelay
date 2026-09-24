using SmsRelay.Models;

namespace SmsRelay.Services;

public interface ISettingsService
{
    Task<RelaySettings> GetAsync();
    Task SaveAsync(RelaySettings settings, IReadOnlyDictionary<Guid, string>? tokenValues = null);
    Task<string?> GetTokenAsync(Guid tokenId);
    Task<IReadOnlyList<GotifyDestination>> GetDestinationsAsync();
    Task<IReadOnlyList<DeliveryTarget>> GetTargetsAsync(IEnumerable<Guid> tokenIds);
    Task<IReadOnlyList<DeliveryTarget>> GetAutomaticTargetsAsync(string sender, string body);
    Task<IReadOnlyList<RuleMatchedMessage>> GetRuleMatchedMessagesAsync(IEnumerable<SmsRecord> messages, IEnumerable<Guid> ruleGroupIds);
}

public interface IQueueService
{
    Task<bool> EnqueueIncomingAsync(string sender, string body, DateTimeOffset receivedAt, IEnumerable<DeliveryTarget> targets);
    Task EnqueueManualAsync(IEnumerable<SmsRecord> messages, IEnumerable<DeliveryTarget> targets);
    Task EnqueueManualAsync(IEnumerable<ManualDelivery> deliveries);
    Task<IReadOnlyList<QueueItem>> GetAsync();
    Task RemoveAsync(Guid id);
    Task ClearAllAsync();
    Task RetryAsync(Guid id);
    Task<IReadOnlyList<QueueItem>> GetDueAsync(DateTimeOffset now);
    Task UpdateAsync(QueueItem item);
}

public interface IGotifyClient
{
    Task SendAsync(QueueItem item, CancellationToken cancellationToken);
    Task TestAsync(DeliveryTarget target, CancellationToken cancellationToken);
}

public interface IQueueProcessor { Task ProcessAsync(CancellationToken cancellationToken = default); }
public interface ISmsHistoryService
{
    Task<SmsPage> SearchAsync(DateTime? startDate, int days, string? query, int offset, int limit = 100);
    Task<DateTime?> FindAdjacentMessageDateAsync(DateTime startDate, int days, int direction);
}
