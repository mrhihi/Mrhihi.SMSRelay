using SmsRelay.Models;

namespace SmsRelay.Services;

public sealed class QueueProcessor(IQueueService queue, IGotifyClient gotify) : IQueueProcessor
{
    public async Task ProcessAsync(CancellationToken cancellationToken = default)
    {
        foreach (var item in await queue.GetDueAsync(DateTimeOffset.UtcNow))
        {
            try
            {
                item.Status = DeliveryStatus.Sending;
                await queue.UpdateAsync(item);
                await gotify.SendAsync(item, cancellationToken);
                item.Status = DeliveryStatus.Sent;
                item.Body = string.Empty; // successful history deliberately does not retain SMS content
                item.LastError = null;
                item.NextAttemptAt = null;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                item.Status = DeliveryStatus.Failed;
                item.AttemptCount++;
                item.LastError = ex.Message;
                item.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(Math.Min(360, Math.Pow(2, Math.Min(item.AttemptCount, 8))));
                Platforms.Android.AndroidJobScheduler.Schedule(item.NextAttemptAt.Value - DateTimeOffset.UtcNow);
            }
            await queue.UpdateAsync(item);
        }
    }
}
