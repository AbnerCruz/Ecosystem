using System.Text.Json.Nodes;
using Avalonia.Controls;
using Urbe.Core.Assistant;

namespace Urbe.Client;

/// <summary>
/// CST-217: what a consulted or delegated specialist does shows inside the card of the call that asked for it, as
/// nested steps, while it works (and afterwards, folded). The specialist's own conversation keeps the full record.
/// </summary>
public sealed partial class AssistantPanel
{
    /// <summary>The consultation/delegation call each conversation is running now (by conversation id).</summary>
    private readonly Dictionary<string, string> _delegationCall = new(StringComparer.Ordinal);
    /// <summary>The specialist's steps under each call (by call id), in order.</summary>
    private readonly Dictionary<string, List<string>> _nested = new(StringComparer.Ordinal);

    /// <summary>The nested steps under a call (tests).</summary>
    public IReadOnlyList<string> NestedSteps(string callId) => _nested.TryGetValue(callId, out var steps) ? steps : [];

    private void NestedStep(AiConversation conversation, AiAgentEvent e)
    {
        if (e.Type == "tool_status" && e.Status == "running" && e.Call is { Name: "consult_agent" or AssistantDelegateTools.DelegateName } asked)
            _delegationCall[conversation.Id] = asked.Id;
        if (e.Type == "tool" && e.Call is { Name: "consult_agent" or AssistantDelegateTools.DelegateName } done && _delegationCall.GetValueOrDefault(conversation.Id) == done.Id)
            _delegationCall.Remove(conversation.Id);
        if (e.Type != "tool" || e.Call is not { } step || conversation.OriginConversationId is not { } origin ||
            !_delegationCall.TryGetValue(origin, out var parentCall)) return;
        if (!_nested.TryGetValue(parentCall, out var steps)) _nested[parentCall] = steps = [];
        var tool = ToolByName(step.Name);
        var label = tool is not null ? SafeLabel(tool, step.Input) : step.Name;
        var who = AgentNames.CallName(AssistantAgent.AgentById(conversation.AgentId, _cfg.CustomAgents), _cfg);
        steps.Add(label + (e.Result?.Status == "rejected" ? " — recusado" : e.Result?.IsError == true ? " — falhou" : ""));
        _progress[parentCall] = who + " · " + steps.Count + " passo(s) · " + label;
        if (_conv.Id == origin && MessageOf(parentCall) is { } message) Schedule(message);
    }

    private Control? NestedView(string callId)
    {
        if (!_nested.TryGetValue(callId, out var steps) || steps.Count == 0) return null;
        return Lazy(Label("Passos do especialista (" + steps.Count + ")", 12, UrbeTheme.Text3), () =>
        {
            var list = new StackPanel { Spacing = 2 };
            foreach (var s in steps) list.Children.Add(Wrap("↳ " + s, 12, UrbeTheme.Text2));
            return list;
        });
    }
}
