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
[MetaData("android.app.PROPERTY_SPECIAL_USE_FGS_SUBTYPE",
    Value = "User-initiated local AI agent execution and review of vault artifacts while the Urbe UI is backgrounded")]
public sealed class AssistantRunService : Service
{
    private const string ChannelActive = "urbe-ai-active";
    private const string ChannelEvents = "urbe-ai-events";
    private const string ActionStart = "app.urbe.ai.RUNNING";
    private const int ActiveId = 8101;
    private const int EventId = 8102;

    private static AssistantActivity? _latest;
    private static AssistantRunService? _instance;

    public override IBinder? OnBind(Intent? intent) => null;

    private static bool IsActive(AssistantActivity activity) =>
        activity.Kind is AssistantActivityKind.Running or AssistantActivityKind.ApprovalNeeded;

    /// <summary>Called from the assistant's UI-thread execution lifecycle.</summary>
    public static void Publish(AssistantActivity activity)
    {
        var context = Android.App.Application.Context;
        try
        {
            if (!IsActive(activity))
            {
                _latest = null;
                context.StopService(new Intent(context, typeof(AssistantRunService)));
                if (!MainActivity.IsVisibleToUser && activity.Kind != AssistantActivityKind.Cancelled)
                    Notify(context, activity);
                return;
            }

            _latest = activity;
            if (_instance is { } service)
            {
                // An approval updates the existing foreground notification. It must
                // not create another foreground-start obligation while backgrounded.
                service.Promote(activity);
            }
            else if (MainActivity.IsVisibleToUser)
            {
                // This command originates in a visible Activity. StartService is
                // allowed here; OnCreate promotes it immediately. StartForegroundService
                // arms a process-fatal deadline while the same main looper is still
                // preparing the AI request (or the run may already have completed).
                context.StartService(new Intent(context, typeof(AssistantRunService)).SetAction(ActionStart));
            }
            else
            {
                // Do not launch a new service from the background without an exemption.
                CrashLog.Write(new InvalidOperationException(
                    "O serviço do Assistente não foi iniciado: o aplicativo não está visível."), "assistant-background");
            }
            if (!MainActivity.IsVisibleToUser && activity.Kind == AssistantActivityKind.ApprovalNeeded)
                Notify(context, activity);
        }
        catch (Exception error) when (error is Java.Lang.Exception or System.Security.SecurityException)
        {
            CrashLog.Write(error, "assistant-background");
            // The visible task can continue; background continuity is not guaranteed.
        }
    }

    public override void OnCreate()
    {
        base.OnCreate();
        _instance = this;
        if (_latest is { } run) Promote(run);
        else StopSelf(); // The run finished before Android created the service.
    }

    [return: Android.Runtime.GeneratedEnum]
    public override StartCommandResult OnStartCommand(Intent? intent, [Android.Runtime.GeneratedEnum] StartCommandFlags flags, int startId)
    {
        if (_latest is { } run) Promote(run);
        else StopSelf();
        // Never restart an agent without its original in-memory vault and approvals.
        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        if (ReferenceEquals(_instance, this)) _instance = null;
        base.OnDestroy();
    }

    private void Promote(AssistantActivity run)
    {
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
