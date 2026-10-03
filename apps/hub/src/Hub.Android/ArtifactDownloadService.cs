using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Hub.Core;

namespace HubApp;

internal static class HubDownloads
{
    internal static readonly ArtifactTransferSession Session = new();
    static readonly object MarkerGate = new();
    internal static string Directory(Context context) => Path.Combine(context.CacheDir!.AbsolutePath, "hub-artifacts");
    internal static string Marker(Context context) => Path.Combine(Directory(context), "active-transfer");
    internal static void WriteMarker(Context context, Guid operation)
    {
        lock (MarkerGate)
        {
            System.IO.Directory.CreateDirectory(Directory(context));
            File.WriteAllText(Marker(context), operation.ToString());
        }
    }
    internal static void ClearMarker(Context context, Guid operation)
    {
        lock (MarkerGate)
            if (File.Exists(Marker(context)) && File.ReadAllText(Marker(context)) == operation.ToString())
                File.Delete(Marker(context));
    }
    internal static void Recover(Context context)
    {
        if (File.Exists(Marker(context))) Session.RecoverInterrupted();
    }
}

// Serviço interno de um download explicitamente solicitado; não é Service/capability compartilhado do Ecosystem.
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeDataSync)]
public sealed class ArtifactDownloadService : Service
{
    internal const string StartAction = "io.ecosystem.hub.DOWNLOAD";
    const string CancelAction = "io.ecosystem.hub.CANCEL_DOWNLOAD";
    internal const string OperationExtra = "operation";
    const string ChannelId = "hub-downloads";
    const int NotificationId = 4202;
    static readonly HttpClient Http = new(new HttpClientHandler { UseCookies = false })
        { Timeout = Timeout.InfiniteTimeSpan };
    readonly Handler _main = new(Looper.MainLooper!);
    Guid _operation;
    long _lastNotification;
    bool _destroyed;

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnCreate()
    {
        base.OnCreate();
        var manager = (NotificationManager)GetSystemService(NotificationService)!;
        manager.CreateNotificationChannel(new NotificationChannel(ChannelId, "Downloads do Hub", NotificationImportance.Low)
            { Description = "Progresso, cancelamento e resultado dos APKs solicitados." });
        HubDownloads.Session.Changed += Changed;
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (!Guid.TryParse(intent?.GetStringExtra(OperationExtra), out var operation))
        { if (!HubDownloads.Session.Status.Active) StopSelf(startId); return StartCommandResult.NotSticky; }
        if (intent!.Action == CancelAction)
        {
            HubDownloads.Session.Cancel(operation);
            if (!HubDownloads.Session.Status.Active) StopSelf(startId);
            return StartCommandResult.NotSticky;
        }
        var status = HubDownloads.Session.Status;
        if (intent.Action != StartAction || status.Operation != operation || !status.Active)
        { if (!status.Active) StopSelf(startId); return StartCommandResult.NotSticky; }
        _operation = operation;
        try
        {
            StartForeground(NotificationId, BuildNotification(status));
            if (status.Phase == ArtifactTransferPhase.Queued)
            {
                HubDownloads.WriteMarker(this, operation);
                _ = RunAsync(operation, startId);
            }
        }
        catch (Exception)
        {
            HubDownloads.Session.FailQueued(operation, "Não foi possível iniciar o serviço de download. Volte ao Hub e tente novamente.");
            HubDownloads.Session.Cancel(operation, "Serviço de download indisponível; tente novamente.");
            StopForeground(StopForegroundFlags.Remove);
            StopSelf(startId);
        }
        return StartCommandResult.NotSticky; // processo morto nunca reinicia transferência silenciosamente
    }

    async Task RunAsync(Guid operation, int startId)
    {
        var downloader = new ArtifactDownloader(Http, HubDownloads.Directory(this));
        await HubDownloads.Session.ExecuteAsync(operation,
            (choice, progress, ct) => downloader.DownloadAsync(choice, progress, ct)).ConfigureAwait(false);
        try { HubDownloads.ClearMarker(this, operation); }
        catch (IOException) { /* marcador sem resultado recuperável será explicado no próximo processo */ }
        catch (UnauthorizedAccessException) { }
        _main.Post(() =>
        {
            if (_destroyed || HubDownloads.Session.Status.Operation != operation || HubDownloads.Session.Status.Active) return;
            Publish(HubDownloads.Session.Status);
            StopForeground(StopForegroundFlags.Detach);
            // Cancels podem ter outro startId; só pare se esta ainda for a operação atual.
            StopSelf();
        });
    }

    void Changed() => _main.Post(() =>
    {
        if (_destroyed) return;
        var status = HubDownloads.Session.Status;
        if (status.Operation != _operation) return;
        long now = SystemClock.ElapsedRealtime();
        if (status.Active && !status.Cancelling && now - _lastNotification < 500) return;
        _lastNotification = now;
        Publish(status);
    });

    void Publish(ArtifactTransferStatus status)
    {
        try { ((NotificationManager)GetSystemService(NotificationService)!).Notify(NotificationId, BuildNotification(status)); }
        catch (Exception) { /* notificação bloqueada não falsifica o resultado da transferência */ }
    }

    Notification BuildNotification(ArtifactTransferStatus status)
    {
        var open = PendingIntent.GetActivity(this, 0, new Intent(this, typeof(MainActivity)),
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        var title = status.Active ? "Baixando e conferindo APK" : "Download do Hub";
        var message = status.Cancelling ? "Cancelando e descartando o arquivo incompleto…"
            : status.Result is { } result ? $"{status.Choice?.ProductName} · {status.Choice?.Asset.Name}: {result.Message}"
                : $"{status.Choice?.ProductName} · {status.Choice?.Asset.Name}";
        var builder = new Notification.Builder(this, ChannelId)
            .SetSmallIcon(global::Android.Resource.Drawable.StatSysDownload)
            .SetContentTitle(title).SetContentText(message).SetStyle(new Notification.BigTextStyle().BigText(message)).SetContentIntent(open)
            .SetOnlyAlertOnce(true).SetOngoing(status.Active).SetAutoCancel(!status.Active);
        if (status.Active)
        {
            int percent = status.Progress is { ExpectedBytes: > 0 } p
                ? (int)Math.Clamp(p.Bytes * 100 / p.ExpectedBytes, 0, 100) : 0;
            builder.SetProgress(100, percent, status.Progress is null);
            if (!status.Cancelling)
            {
                var cancel = PendingIntent.GetService(this, 1,
                    new Intent(this, typeof(ArtifactDownloadService)).SetAction(CancelAction)
                        .PutExtra(OperationExtra, status.Operation.ToString()),
                    PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
                builder.AddAction(new Notification.Action.Builder(
                    global::Android.Resource.Drawable.IcMenuCloseClearCancel, "Cancelar", cancel).Build());
            }
        }
        return builder.Build();
    }

    public override void OnTimeout(int startId, ForegroundService fgsType)
    {
        HubDownloads.Session.Cancel(_operation, "O Android limitou o serviço de segundo plano; tente novamente.");
        StopForeground(StopForegroundFlags.Remove);
        StopSelf();
        base.OnTimeout(startId, fgsType);
    }

    public override void OnDestroy()
    {
        _destroyed = true;
        HubDownloads.Session.Changed -= Changed;
        HubDownloads.Session.Cancel(_operation, "Serviço interrompido pelo Android; tente novamente.");
        base.OnDestroy();
    }
}
