using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace Lunet.Android;

/// <summary>
/// Long-running, user-initiated network/file work. Avoids attaching an agent task to an Activity.
/// Not sticky: Android process death cannot safely restart a tool-writing run without a checkpoint replay.
/// </summary>
[Service(Name = "io.lunet.studio.LunetAiForegroundService", Exported = false,
    ForegroundServiceType = ForegroundService.TypeDataSync)]
internal sealed class LunetAiForegroundService : Service
{
    private const int NotificationId = 4108;
    private const string ChannelId = "lunet_ai_work";

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        // Must execute synchronously, before any HTTP/model/build work, to avoid
        // ForegroundServiceDidNotStartInTimeException on Android 12+.
        try
        {
            var notifications = (NotificationManager?)GetSystemService(NotificationService);
            notifications?.CreateNotificationChannel(new NotificationChannel(ChannelId, "Lunet IA",
                NotificationImportance.Low));
            var open = new Intent(this, typeof(MainActivity));
            open.AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
            var pending = PendingIntent.GetActivity(this, 0, open,
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
            var notification = new Notification.Builder(this, ChannelId)
                .SetSmallIcon(global::Android.Resource.Drawable.StatNotifySync)
                .SetContentTitle("Lunet IA")
                .SetContentText("Trabalhando no projeto " + (LunetAiRuns.Current?.ProjectName ?? "atual"))
                .SetContentIntent(pending)
                .SetOngoing(true)
                .Build();
            StartForeground(NotificationId, notification, ForegroundService.TypeDataSync);
        }
        catch (Exception ex)
        {
            LunetAiRuns.Current?.FailBeforeStart("Não foi possível iniciar o serviço em primeiro plano: " + ex.Message);
            StopSelf(startId);
            return StartCommandResult.NotSticky;
        }

        var work = LunetAiRuns.RunPending();
        if (work is null)
        {
            StopForeground(StopForegroundFlags.Remove);
            StopSelf(startId);
            return StartCommandResult.NotSticky;
        }
        _ = work.ContinueWith(_ =>
        {
            new Handler(Looper.MainLooper!).Post(() =>
            {
                StopForeground(StopForegroundFlags.Remove);
                StopSelf(startId);
            });
        }, TaskScheduler.Default);
        return StartCommandResult.NotSticky;
    }
}
