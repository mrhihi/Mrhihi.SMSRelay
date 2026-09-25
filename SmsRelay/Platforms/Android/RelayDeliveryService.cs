using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using SmsRelay.Services;

namespace SmsRelay.Platforms.Android;

[Service(Enabled = true, Exported = false, ForegroundServiceType = ForegroundService.TypeDataSync)]
public sealed class RelayDeliveryService : Service
{
    private const string ChannelId = "sms-relay-delivery";
    private const int NotificationId = 20832;
    private static int _activeWorkers;

    public static bool IsBatteryOptimizationIgnored()
    {
        var context = global::Android.App.Application.Context;
        var power = context.GetSystemService(Context.PowerService) as PowerManager;
        return power?.IsIgnoringBatteryOptimizations(context.PackageName) == true;
    }

    // SMS broadcasts are allowed to persist quickly, but scheduled jobs can be deferred in Doze.
    // A battery-optimization exemption permits this short data-sync foreground service to run now.
    public static void RequestDelivery()
    {
        var context = global::Android.App.Application.Context;
        try
        {
            context.StartForegroundService(new Intent(context, typeof(RelayDeliveryService)));
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("SmsRelay", $"Unable to start immediate delivery service: {ex.Message}");
        }
        // Keep a constrained job as a recovery path if the foreground process is killed.
        AndroidJobScheduler.Schedule();
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var notification = CreateNotification();
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            StartForeground(NotificationId, notification, ForegroundService.TypeDataSync);
        else
            StartForeground(NotificationId, notification);
        Interlocked.Increment(ref _activeWorkers);
        _ = DeliverAsync(startId);
        return StartCommandResult.NotSticky;
    }

    private async Task DeliverAsync(int startId)
    {
        try
        {
            await ServiceRegistry.Get<IQueueProcessor>().ProcessAsync();
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("SmsRelay", $"Immediate delivery failed: {ex.Message}");
            AndroidJobScheduler.Schedule();
        }
        finally
        {
            if (Interlocked.Decrement(ref _activeWorkers) == 0)
            {
                StopForeground(StopForegroundFlags.Remove);
                StopSelf(startId);
            }
        }
    }

    private Notification CreateNotification()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var manager = GetSystemService(NotificationService) as NotificationManager;
            manager?.CreateNotificationChannel(new NotificationChannel(ChannelId, "SMS Relay 轉發", NotificationImportance.Low)
            {
                Description = "收到 SMS 時的短暫轉發通知"
            });
        }

        var launch = PendingIntent.GetActivity(this, 0, new Intent(this, typeof(MainActivity)), PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        return new Notification.Builder(this, ChannelId)
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetContentTitle("SMS Relay 正在轉發")
            .SetContentText("正在安全傳送待處理的簡訊。")
            .SetContentIntent(launch)
            .SetOngoing(true)
            .Build();
    }
}
