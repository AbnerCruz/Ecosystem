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

    private const string HoldExtra = "hold_game";
    private const int DoneNotificationId = 4109;
    private static LunetAiForegroundService? _holding;
    private static int _holds;

    /// <summary>
    /// The shell's AI run (H4/M7): the run lives in the shell, the service only keeps the process in the foreground with
    /// a notification while it lasts. Disposing ends it; when the app is not visible, a "terminou" notice is posted.
    /// </summary>
    public static IDisposable? Hold(global::Android.Content.Context context, string gameName, Func<bool> appVisible)
    {
        try
        {
            var intent = new Intent(context, typeof(LunetAiForegroundService)).PutExtra(HoldExtra, gameName);
            context.StartForegroundService(intent);
            Interlocked.Increment(ref _holds);
        }
        catch (Exception ex) when (ex is Java.Lang.Exception or InvalidOperationException)
        {
            return null; // Android refused: the run continues while the app is open, without the background guarantee
        }
        return new Release(context, gameName, appVisible);
    }

    private sealed class Release(global::Android.Content.Context context, string gameName, Func<bool> appVisible) : IDisposable
    {
        private int _done;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) == 1) return;
            new Handler(Looper.MainLooper!).Post(() =>
            {
                if (Interlocked.Decrement(ref _holds) == 0 && _holding is { } service)
                {
                    service.StopForeground(StopForegroundFlags.Remove);
                    service.StopSelf();
                    _holding = null;
                }
                if (!appVisible()) NotifyDone(context, gameName);
            });
        }
    }

    private static void NotifyDone(global::Android.Content.Context context, string gameName)
    {
        try
        {
            var notifications = (NotificationManager?)context.GetSystemService(NotificationService);
            if (notifications is null || (OperatingSystem.IsAndroidVersionAtLeast(33) && !notifications.AreNotificationsEnabled())) return;
            notifications.CreateNotificationChannel(new NotificationChannel(ChannelId, "Lunet IA", NotificationImportance.Low));
            var open = new Intent(context, typeof(ShellActivity)).AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
            var pending = PendingIntent.GetActivity(context, 1, open, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
            notifications.Notify(DoneNotificationId, new Notification.Builder(context, ChannelId)
                .SetSmallIcon(global::Android.Resource.Drawable.StatNotifySync)
                .SetContentTitle("Lunet IA terminou")
                .SetContentText("A resposta sobre " + gameName + " está pronta.")
                .SetContentIntent(pending)
                .SetAutoCancel(true)
                .Build());
        }
        catch (Java.Lang.Exception) { } // a notice that cannot be shown never breaks the run
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.GetStringExtra(HoldExtra) is { } holdGame)
        {
            try
            {
                var notifications = (NotificationManager?)GetSystemService(NotificationService);
                notifications?.CreateNotificationChannel(new NotificationChannel(ChannelId, "Lunet IA", NotificationImportance.Low));
                var open = new Intent(this, typeof(ShellActivity)).AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
                var pending = PendingIntent.GetActivity(this, 0, open, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
                StartForeground(NotificationId, new Notification.Builder(this, ChannelId)
                    .SetSmallIcon(global::Android.Resource.Drawable.StatNotifySync)
                    .SetContentTitle("Lunet IA")
                    .SetContentText("Trabalhando em " + holdGame)
                    .SetContentIntent(pending)
                    .SetOngoing(true)
                    .Build(), ForegroundService.TypeDataSync);
                _holding = this;
                if (Volatile.Read(ref _holds) == 0) { StopForeground(StopForegroundFlags.Remove); StopSelf(startId); _holding = null; } // already over
            }
            catch (Exception) { StopSelf(startId); }
            return StartCommandResult.NotSticky;
        }
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
