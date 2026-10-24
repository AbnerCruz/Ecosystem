namespace Urbe.Client;

/// <summary>Platform-independent lifecycle of a user-initiated AI agent run.</summary>
/// <remarks>
/// Android uses a user-visible foreground service and notifications; desktop hosts may omit the bridge.
/// A foreground service protects an active process from ordinary background suspension, but cannot
/// guarantee survival after a force-stop, device restart or operating-system process termination.
/// </remarks>
public enum AssistantActivityKind { Running, ApprovalNeeded, QuestionNeeded, Completed, Failed, Cancelled }

public sealed record AssistantActivity(AssistantActivityKind Kind, string Agent, string ConversationId);

public static class PlatformAssistantActivity
{
    /// <summary>The native host registers notification and foreground-service handling here.</summary>
    public static Action<AssistantActivity>? Publish { get; set; }

    /// <summary>Ask once for notification display permission while the user is interacting with the app.</summary>
    public static Action? RequestNotificationPermission { get; set; }

    /// <summary>Invoked when the user taps an agent notification (the app still decides what to open).</summary>
    public static Action? OpenAssistant { get; set; }

    public static void OpenFromNotification() => OpenAssistant?.Invoke();
}
