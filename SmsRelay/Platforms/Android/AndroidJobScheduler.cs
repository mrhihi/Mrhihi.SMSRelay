using Android.App;
using Android.App.Job;
using Android.Content;
using SmsRelay.Services;

namespace SmsRelay.Platforms.Android;

public static class AndroidJobScheduler
{
    private const int JobId = 20831;
    public static void Schedule(TimeSpan? delay = null)
    {
        var context = global::Android.App.Application.Context;
        var scheduler = (JobScheduler?)context.GetSystemService(Context.JobSchedulerService);
        if (scheduler is null) return;
        var component = new ComponentName(context, Java.Lang.Class.FromType(typeof(RelayJobService)));
        var job = new JobInfo.Builder(JobId, component)
            .SetRequiredNetworkType(NetworkType.Any)
            .SetBackoffCriteria(30_000, BackoffPolicy.Exponential)
            .SetMinimumLatency((long)Math.Max(0, (delay ?? TimeSpan.Zero).TotalMilliseconds))
            .Build();
        scheduler.Schedule(job);
    }
}

[Service(Enabled = true, Exported = false, Permission = "android.permission.BIND_JOB_SERVICE")]
public sealed class RelayJobService : JobService
{
    public override bool OnStartJob(JobParameters? parameters)
    {
        _ = Task.Run(async () =>
        {
            var retry = false;
            try { await ServiceRegistry.Get<IQueueProcessor>().ProcessAsync(); }
            catch (Exception ex) { retry = true; global::Android.Util.Log.Warn("SmsRelay", $"Queue job failed: {ex.Message}"); }
            finally { JobFinished(parameters, retry); }
        });
        return true;
    }
    public override bool OnStopJob(JobParameters? parameters) => true;
}
