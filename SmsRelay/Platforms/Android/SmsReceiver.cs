using Android.Content;
using Android.Provider;
using SmsRelay.Services;

namespace SmsRelay.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = true, DirectBootAware = false)]
[global::Android.App.IntentFilter(new[] { Telephony.Sms.Intents.SmsReceivedAction }, Priority = 999)]
public sealed class SmsReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action != Telephony.Sms.Intents.SmsReceivedAction) return;
        var pending = GoAsync();
        _ = Task.Run(async () =>
        {
            try
            {
                var messages = Telephony.Sms.Intents.GetMessagesFromIntent(intent);
                if (messages is null || messages.Length == 0) return;
                var sender = messages[0]?.OriginatingAddress ?? "未知來源";
                var text = string.Concat(messages.Where(x => x is not null).Select(x => x!.MessageBody));
                var at = DateTimeOffset.FromUnixTimeMilliseconds(messages[0]?.TimestampMillis ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                if (await ServiceRegistry.Get<ISettingsService>().IsSmsAllowedAsync(sender, text))
                    await ServiceRegistry.Get<IQueueService>().EnqueueIncomingAsync(sender, text, at);
            }
            catch (Exception ex) { global::Android.Util.Log.Warn("SmsRelay", $"Unable to process incoming SMS: {ex.Message}"); }
            finally { pending.Finish(); }
        });
    }
}
