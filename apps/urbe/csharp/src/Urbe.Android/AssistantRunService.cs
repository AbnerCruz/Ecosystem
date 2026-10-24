using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Urbe.Client;

[assembly: UsesPermission("android.permission.FOREGROUND_SERVICE_SPECIAL_USE")]

namespace Urbe.AndroidHost;

/// <summary>
/// User-initiated AI work that should continue when the activity is not visible.
/// The running task still belongs to the existing AssistantPanel/Urbe.Core session;
/// the service keeps its process in the foreground rather than silently moving
/// the agent, credentials or vault ownership into another runtime.
/// </summary>
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeSpecialUse)]
[MetaData(Name = "android.app.PROPERTY_SPECIAL_USE_FGS_SUBTYPE",
    Value = "User-initiated local AI agent execution and review of vault artifacts while the Urbe UI is backgrounded")]
public sealed class AssistantRunService : Service
{
    private const string ChannelActive = "urbe-ai-active";
    private const string ChannelEvents = "urbe-ai-events";
    private const string ActionStart = "app.urbe.ai.RUNNING";
    private const int ActiveId = 8101;
    private const int EventId = 8102;

    private static AssistantActivity? _latest;

    public override IBinder? OnBind(Intent? intent) => null;

    /// <summary>Called from the assistant's UI-thread execution lifecycle.</summary>
    public static void Publish(AssistantActivity activity)
    {
        _latest = activity;
        var context = Android.App.Application.Context;
        if (activity.Kind is AssistantActivityKind.Completed or AssistantActivityKind.Failed or AssistantActivityKind.Cancelled)
        {
            if (!MainActivity.IsVisibleToUser && activity.Kind != AssistantActivityKind.Cancelled)
                Notify(context, activity);
            context.StopService(new Intent(context, typeof(AssistantRunService)));
            return;
        }

        // A run is started only by a user command while the activity is foreground.
        // Changes to approval/question state update the existing service without starting another run.
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
                context.StartForegroundService(new Intent(context, typeof(AssistantRunService)).SetAction(ActionStart));
            else
                context.StartService(new Intent(context, typeof(AssistantRunService)).SetAction(ActionStart));
        }
        catch (Exception error) when (error is Java.Lang.Exception or System.Security.SecurityException)
        {
            CrashLog.Write(error, "assistant-background");
            // Never misrepresent background continuity: the in-app task can keep running
            // while visible, but the OS may suspend it when the app is backgrounded.
        }
        if (!MainActivity.IsVisibleToUser && activity.Kind is AssistantActivityKind.ApprovalNeeded or AssistantActivityKind.QuestionNeeded)
            Notify(context, activity);
    }

    [return: Android.Runtime.GeneratedEnum]
    public override StartCommandResult OnStartCommand(Intent? intent, [Android.Runtime.GeneratedEnum] StartCommandFlags flags, int startId)
    {
        var run = _latest;
        if (run is null)
        {
            StopSelf();
            return StartCommandResult.NotSticky;
        }
        try
        {
            var notification = Build(this, run, ongoing: true);
            if (OperatingSystem.IsAndroidVersionAtLeast(34))
                StartForeground(ActiveId, notification, ForegroundService.TypeSpecialUse);
            else
                StartForeground(ActiveId, notification);
        }
        catch (Exception error) when (error is Java.Lang.Exception or System.Security.SecurityException)
        {
            CrashLog.Write(error, "assistant-foreground");
            StopSelf();
        }
        // The Android system must not restart an agent without its original in-memory vault,
        // approvals and provider context. Recovery from process death is a separate task.
        return StartCommandResult.NotSticky;
    }

    private static void Channels(Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;
        if (context.GetSystemService(NotificationService) is not NotificationManager manager) return;
        manager.CreateNotificationChannel(new NotificationChannel(ChannelActive, "Assistente em execução", NotificationImportance.Low));
        manager.CreateNotificationChannel(new NotificationChannel(ChannelEvents, "Assistente: sua atenção", NotificationImportance.Default));
    }

    private static Notification Build(Context context, AssistantActivity run, bool ongoing)
    {
        Channels(context);
        var channel = ongoing ? ChannelActive : ChannelEvents;
        var title = run.Kind switch
        {
            AssistantActivityKind.Running => run.Agent + " está trabalhando",
            AssistantActivityKind.ApprovalNeeded => run.Agent + " precisa de aprovação",
            AssistantActivityKind.QuestionNeeded => run.Agent + " tem uma pergunta",
            AssistantActivityKind.Completed => run.Agent + " concluiu a tarefa",
            AssistantActivityKind.Failed => run.Agent + " encontrou um problema",
            _ => "Assistente do Urbe"
        };
        var description = run.Kind switch
        {
            AssistantActivityKind.Running => "A execução continua em segundo plano. Toque para acompanhar.",
            AssistantActivityKind.ApprovalNeeded => "Revise a alteração antes de autorizar.",
            AssistantActivityKind.QuestionNeeded => "Responda para o agente continuar.",
            AssistantActivityKind.Completed => "Abra a conversa para conferir o resultado.",
            AssistantActivityKind.Failed => "Abra a conversa para ver o erro.",
            _ => "Abra o Assistente."
        };
        var launch = new Intent(context, typeof(MainActivity))
            .SetAction("app.urbe.ai.OPEN")
            .AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        var tap = PendingIntent.GetActivity(context, 81, launch,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        var builder = OperatingSystem.IsAndroidVersionAtLeast(26)
            ? new Notification.Builder(context, channel)
            : new Notification.Builder(context);
        return builder.SetSmallIcon(Resource.Drawable.urbe_voice_done)
            .SetContentTitle(title).SetContentText(description)
            .SetContentIntent(tap).SetOngoing(ongoing).SetOnlyAlertOnce(ongoing)
            .SetAutoCancel(!ongoing).Build()!;
    }

    private static void Notify(Context context, AssistantActivity run)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33) &&
            context.CheckSelfPermission("android.permission.POST_NOTIFICATIONS") != Permission.Granted)
            return;
        if (context.GetSystemService(NotificationService) is NotificationManager manager)
            manager.Notify(EventId, Build(context, run, ongoing: false));
    }
}
