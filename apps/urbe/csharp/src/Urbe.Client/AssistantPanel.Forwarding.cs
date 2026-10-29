using Avalonia.Controls;
using Avalonia.Threading;
using Urbe.Core.Assistant;

namespace Urbe.Client;

/// <summary>
/// CST-091: what a consulted specialist needs from the person — a question (<c>ask_user</c>) or an approval — shows in
/// the conversation that asked for the consultation, and the answer goes back to the specialist's own conversation;
/// the asking agent waits in its consult_agent call meanwhile. Forms still ask for the specialist's conversation.
/// </summary>
public sealed partial class AssistantPanel
{
    private sealed record ForwardedApproval(AiConversation Child, AiApprovalRequest Request, TaskCompletionSource<AiApproval> Answer);
    private sealed record ForwardedQuestion(AiConversation Child, AiChatBlock Call, TaskCompletionSource<string> Answer);

    // by the asking conversation's id
    private readonly Dictionary<string, ForwardedApproval> _forwardedApprovals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ForwardedQuestion> _forwardedQuestions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Control> _forwardViews = new(StringComparer.Ordinal);

    /// <summary>A consulted specialist waits for the person in some conversation (a question or an approval).</summary>
    public bool SpecialistWaiting => _forwardedApprovals.Count > 0 || _forwardedQuestions.Count > 0;

    private string SpecialistName(AiConversation child) =>
        AgentNames.CallName(AssistantAgent.AgentById(child.AgentId, _cfg.CustomAgents), _cfg);

    /// <summary>True when the request is shown in the conversation on screen.</summary>
    private bool ForwardApproval(AiConversation child, AiApprovalRequest request, TaskCompletionSource<AiApproval> answer, CancellationToken cancel)
    {
        if (child.OriginConversationId is not { } origin) return false;
        _forwardedApprovals[origin] = new(child, request, answer);
        cancel.Register(() => Dispatcher.UIThread.Post(() =>
        {
            if (_forwardedApprovals.TryGetValue(origin, out var f) && ReferenceEquals(f.Request, request)) Unforward(origin, approvals: true);
        }));
        if (origin != _conv.Id) return false;
        RenderAll();
        NotifyCompactChanged();
        if (!IsVisible && !CompactActive) _app.Notify(SpecialistName(child) + " precisa da sua aprovação.");
        return true;
    }

    /// <summary>The specialist asked the person; the asking conversation shows the question and waits for the answer.</summary>
    private async Task<string> AskThroughRequesterAsync(AiConversation child, AiChatBlock call, CancellationToken cancel)
    {
        var origin = child.OriginConversationId ?? throw new InvalidOperationException("Consulta sem conversa de origem.");
        var answer = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _forwardedQuestions[origin] = new(child, call, answer);
        using var registration = cancel.Register(() => Dispatcher.UIThread.Post(() =>
        {
            if (_forwardedQuestions.TryGetValue(origin, out var q) && q.Answer == answer) { Unforward(origin, approvals: false); answer.TrySetCanceled(); }
        }));
        PlatformAssistantActivity.Publish?.Invoke(new AssistantActivity(AssistantActivityKind.QuestionNeeded, SpecialistName(child), origin));
        if (origin == _conv.Id) { RenderAll(); NotifyCompactChanged(); }
        if (origin != _conv.Id || !IsVisible) _app.Notify(SpecialistName(child) + " tem uma pergunta para você.");
        return await answer.Task;
    }

    private void Unforward(string origin, bool approvals)
    {
        if (approvals && _forwardedApprovals.Remove(origin, out var a)) _forwardViews.Remove(a.Request.Call.Id);
        if (!approvals && _forwardedQuestions.Remove(origin, out var q)) _forwardViews.Remove(q.Call.Id);
        if (origin == _conv.Id) RenderAll();
        NotifyCompactChanged();
    }

    private void AnswerForwarded(string origin, ForwardedApproval f, AiApproval answer)
    {
        if (!_forwardedApprovals.TryGetValue(origin, out var current) || !ReferenceEquals(current, f)) return;
        _pendingRuns.Remove(f.Child.Id);
        _approvalDrafts.Remove(f.Request.Call.Id);
        Unforward(origin, approvals: true);
        f.Answer.TrySetResult(answer);
    }

    /// <summary>The same request answered in the specialist's own conversation: the copy here goes away.</summary>
    private void ForgetForwarded(AiApprovalRequest request)
    {
        foreach (var (origin, f) in _forwardedApprovals.ToList())
            if (ReferenceEquals(f.Request, request)) Unforward(origin, approvals: true);
    }

    /// <summary>The cards waiting in the conversation on screen.</summary>
    private IEnumerable<Control> ForwardedCards()
    {
        var origin = _conv.Id;
        if (_forwardedQuestions.TryGetValue(origin, out var q))
            yield return Forwarded(q.Call.Id, "Pergunta de " + SpecialistName(q.Child), SpecialistName(q.Child) + " pergunta (consultada por este agente):", q.Child,
                () => Clarification(q.Call, q.Child, text =>
                {
                    if (!_forwardedQuestions.TryGetValue(origin, out var now) || !ReferenceEquals(now, q)) return;
                    Unforward(origin, approvals: false);
                    q.Answer.TrySetResult(text);
                }));
        if (_forwardedApprovals.TryGetValue(origin, out var f))
            yield return Forwarded(f.Request.Call.Id, "Pedido de " + SpecialistName(f.Child), SpecialistName(f.Child) + " pede uma aprovação (consultada por este agente):", f.Child,
                () => Approval(f.Request, answer => AnswerForwarded(origin, f, answer)));
    }

    private Control Forwarded(string key, string name, string intro, AiConversation child, Func<Control> body)
    {
        if (_forwardViews.TryGetValue(key, out var view)) return view;
        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(Hint(intro));
        content.Children.Add(body());
        content.Children.Add(LinkButton("Abrir a conversa de " + SpecialistName(child), () => OpenConversation(_activeConversations.GetValueOrDefault(child.Id) ?? child)));
        var card = Card(content, UrbeTheme.Accent);
        Avalonia.Automation.AutomationProperties.SetName(card, name);
        _forwardViews[key] = card;
        return card;
    }
}
