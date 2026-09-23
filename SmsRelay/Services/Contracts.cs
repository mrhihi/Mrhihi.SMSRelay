using SmsRelay.Models;

namespace SmsRelay.Services;

public interface ISettingsService
{
    Task<RelaySettings> GetAsync();
    Task SaveAsync(RelaySettings settings, string? gotifyToken);
    Task<string?> GetTokenAsync();
    Task<bool> IsSmsAllowedAsync(string sender, string body);
}

public interface IQueueService
{
    Task<bool> EnqueueIncomingAsync(string sender, string body, DateTimeOffset receivedAt);
    Task EnqueueManualAsync(IEnumerable<SmsRecord> messages);
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
    Task TestAsync(CancellationToken cancellationToken);
}

public interface IQueueProcessor { Task ProcessAsync(CancellationToken cancellationToken = default); }
public interface ISmsHistoryService
{
    Task<SmsPage> SearchAsync(DateTime? startDate, int days, string? query, int offset, int limit = 100);
    Task<DateTime?> FindAdjacentMessageDateAsync(DateTime startDate, int days, int direction);
}
