using SmsRelay.Models;

namespace SmsRelay.Services;

public sealed class QueueProcessor(IQueueService queue, IGotifyClient gotify, ISettingsService settings) : IQueueProcessor
{
    private static readonly SemaphoreSlim ProcessingGate = new(1, 1);

    public async Task ProcessAsync(CancellationToken cancellationToken = default)
    {
        await ProcessingGate.WaitAsync(cancellationToken);
        try
        {
            foreach (var item in await queue.GetDueAsync(DateTimeOffset.UtcNow))
            {
                try
                {
                    // Queue records created before multi-destination support have no target snapshot.
                    if (item.Target is null)
                    {
                        var fallback = (await settings.GetDestinationsAsync()).FirstOrDefault();
                        if (fallback is null) throw new InvalidOperationException("請先設定 Gotify 目的地。");
                        item.Target = (await settings.GetTargetsAsync([fallback.TokenId])).FirstOrDefault()
                            ?? throw new InvalidOperationException("舊傳送紀錄的 Gotify Token 已不存在。");
                    }
                    item.Status = DeliveryStatus.Sending;
                    item.SendingStartedAt = DateTimeOffset.UtcNow;
                    await queue.UpdateAsync(item);
                    await gotify.SendAsync(item, cancellationToken);
                    item.Status = DeliveryStatus.Sent;
                    item.Body = string.Empty; // successful history deliberately does not retain SMS content
                    if (item.Target is not null) item.Target = item.Target with { Token = string.Empty };
                    item.LastError = null;
                    item.NextAttemptAt = null;
                    item.SendingStartedAt = null;
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    item.Status = DeliveryStatus.Failed;
                    item.AttemptCount++;
                    item.SendingStartedAt = null;
                    item.LastError = ex.Message;
                    item.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(Math.Min(360, Math.Pow(2, Math.Min(item.AttemptCount, 8))));
                    Platforms.Android.AndroidJobScheduler.Schedule(item.NextAttemptAt.Value - DateTimeOffset.UtcNow);
                }
                await queue.UpdateAsync(item);
            }
        }
        finally { ProcessingGate.Release(); }
    }
}
