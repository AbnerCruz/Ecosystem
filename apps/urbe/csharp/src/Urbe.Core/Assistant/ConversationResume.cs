namespace Urbe.Core.Assistant;

/// <summary>
/// CST-145: whether an agent's latest conversation is still in use, so coming back to the agent (after a switch or a
/// restart) continues it instead of opening an empty chat. In use: it has messages and something unfinished (messages
/// waiting their turn, a form not yet sent) or it moved within <see cref="Window"/>. Older ones stay in Conversas and a
/// new chat is the practical start.
/// </summary>
public static class ConversationResume
{
    public static readonly TimeSpan Window = TimeSpan.FromHours(2);

    public static bool InUse(AiConversation conversation, long nowMs) =>
        conversation.Messages.Count > 0 &&
        (conversation.QueuedMessages.Count > 0 || conversation.Forms.Any(f => f.State == "pending") || nowMs - conversation.Updated < Window.TotalMilliseconds);
}
