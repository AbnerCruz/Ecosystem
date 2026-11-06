using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client;

/// <summary>
/// CST-216: work cut by the app being ended (Android ends it in the background) comes back as what it is — interrupted
/// work, not a conversation. The panel says what was done and verified in the vault, what was not done and what needs
/// the person, and offers to resume the work (the run goes on from its journal, never repeating an effect) or only to
/// reopen its conversation (the transcript rebuilt, nothing run).
/// </summary>
public sealed partial class AssistantPanel
{
    /// <summary>Runs going on in this process: their journals are open but not interrupted.</summary>
    private readonly HashSet<string> _liveRuns = new(StringComparer.Ordinal);
    private bool _journalsPruned;

    private AssistantRunJournal BeginJournal(AiConversation conv, string runId, string agentId, string model, string? goal, DocumentStore documents)
    {
        // the base the journal's steps follow: the conversation as it is when the run starts (the request included)
        try { _store.SaveConversationVerified(conv); }
        catch (IOException) { }   // the journal keeps the request itself; resuming still works from it
        var request = conv.Messages.LastOrDefault(m => m.Role == "user" && m.Meta?.ToolResults != true);
        var open = EditorInfo().Path;
        var sources = open is { Length: > 0 } && documents.Get(open) is { } doc ? new List<(string, string?)> { (doc.Path, AssistantRunJournal.Sha(doc.Content)) } : null;
        return AssistantRunJournal.Begin(_storage, new AiRunStart(runId, conv.Id, conv.Vault, agentId, model,
            string.IsNullOrWhiteSpace(goal) ? conv.Title : goal!, conv.Messages.Count, request, sources,
            conv.Brief?.Scope is { Count: > 0 } scope ? string.Join(", ", scope) : null, conv.Brief?.Expected, conv.OriginConversationId));
    }

    /// <summary>
    /// The interrupted runs of this vault: journals neither closed nor running now. One whose conversation already has
    /// the run (it ended and was saved, only the closing line was lost) is closed here, silently.
    /// </summary>
    public IReadOnlyList<AssistantRunJournal> InterruptedRuns()
    {
        if (!_loaded) return [];
        if (!_journalsPruned) { _journalsPruned = true; AssistantRunJournal.Prune(_storage); }
        var list = new List<AssistantRunJournal>();
        foreach (var journal in AssistantRunJournal.Interrupted(_storage, _app.VaultName))
        {
            if (_liveRuns.Contains(journal.RunId) || _activeRuns.ContainsKey(journal.ConversationId)) continue;
            if (_store.GetConversation(journal.ConversationId)?.Runs.Any(r => r.Id == journal.RunId) == true) { journal.Close("ended"); continue; }
            list.Add(journal);
        }
        return list;
    }

    // computed when the panel opens and after the person acts on one; not on every render (streaming renders often)
    private Control? _interruptedView;
    private bool _interruptedStale = true;

    private void InterruptedChanged() { _interruptedStale = true; _interruptedView = null; }

    private Control? InterruptedView()
    {
        if (!_interruptedStale) return _interruptedView;
        if (_app.Documents is not { } documents) return null;
        _interruptedStale = false;
        var runs = InterruptedRuns();
        if (runs.Count == 0) return _interruptedView = null;
        var box = new StackPanel { Spacing = 10 };
        foreach (var journal in runs.Take(5))
        {
            var effects = journal.Effects(documents);
            int done = effects.Count(e => e.State == AiRunEffect.Done), notDone = effects.Count(e => e.State == AiRunEffect.NotDone);
            var uncertain = effects.Where(e => e.State == AiRunEffect.Uncertain).ToList();
            var agent = AgentNames.CallName(AssistantAgent.AgentById(journal.AgentId, _cfg.CustomAgents), _cfg);
            var item = new StackPanel { Spacing = 4 };
            item.Children.Add(Wrap((journal.Finished ? "Trabalho concluído, conversa não guardada" : "Trabalho interrompido") + " · " + agent, 13, UrbeTheme.Text, FontWeight.SemiBold));
            item.Children.Add(Wrap(journal.Goal.Length > 160 ? journal.Goal[..158] + "…" : journal.Goal, 13, UrbeTheme.Text2));
            item.Children.Add(Wrap(done + " ações feitas e conferidas · " + notDone + " não feitas" + (uncertain.Count > 0 ? " · " + uncertain.Count + " precisam de você" : ""), 12, UrbeTheme.Text2));
            foreach (var u in uncertain.Take(3)) item.Children.Add(Wrap("• " + u.Detail, 12, "#c0504d"));
            if (journal.ModelCallCut) item.Children.Add(Wrap("Uma etapa do modelo foi cortada no meio e pode ter sido cobrada pelo provedor sem resposta.", 12, UrbeTheme.Text3));
            var actions = new WrapPanel();
            if (!journal.Finished && journal.Origin is null)
            {
                var resume = TextButton("Retomar", () => _ = ResumeRunAsync(journal), primary: true);
                Avalonia.Automation.AutomationProperties.SetName(resume, "Retomar trabalho interrompido");
                actions.Children.Add(resume);
            }
            actions.Children.Add(LinkButton("Abrir conversa", () => ReopenRun(journal)));
            actions.Children.Add(LinkButton("Descartar", () => { journal.Close("discarded"); InterruptedChanged(); RenderAll(); }));
            item.Children.Add(actions);
            box.Children.Add(item);
        }
        var card = Card(box, uncertainAny(runs, documents) ? "#c0504d" : UrbeTheme.Accent);
        Avalonia.Automation.AutomationProperties.SetName(card, "Trabalho interrompido");
        return _interruptedView = card;

        static bool uncertainAny(IReadOnlyList<AssistantRunJournal> runs, DocumentStore documents) => runs.Any(j => j.NeedsYou(documents));
    }

    /// <summary>The run's conversation with its transcript rebuilt from the journal (the base saved when it started).</summary>
    private AiConversation RebuiltConversation(AssistantRunJournal journal, DocumentStore documents)
    {
        var conv = _store.GetConversation(journal.ConversationId) ?? new AiConversation
        {
            Id = journal.ConversationId, AgentId = journal.AgentId, Vault = journal.Vault, Created = journal.Started, Updated = Now(),
            Title = AssistantAgent.TitleFrom(journal.Goal), Policy = _cfg.PolicyFor(journal.AgentId), OriginConversationId = journal.Origin
        };
        conv.Messages = journal.Rebuild(conv.Messages, documents);
        conv.Usage = AiUsage.Add(conv.Usage, journal.Usage);
        return conv;
    }

    /// <summary>Only the conversation: the transcript as far as it went, no run. The journal is closed.</summary>
    public void ReopenRun(AssistantRunJournal journal)
    {
        if (_app.Documents is not { } documents) return;
        var conv = RebuiltConversation(journal, documents);
        conv.Runs.Add(new AiRunRecord
        {
            Id = journal.RunId, Started = journal.Started, Stopped = journal.Finished ? journal.Stopped : "aborted",
            Error = journal.Finished ? null : "O app foi encerrado durante este trabalho. Nada foi repetido; o que foi feito está nos cartões."
        });
        try { _store.SaveConversationVerified(conv); }
        catch (IOException e) { _app.Notify(e.Message); return; }
        journal.Close("reopened");
        InterruptedChanged();
        OpenConversation(conv);
    }

    /// <summary>
    /// The work goes on from where it was: the transcript rebuilt, a note to the model of what is done, not done and
    /// unverifiable, and the same run (its journal) continued, so nothing done before is done again. Something that
    /// cannot be verified is shown to the person first.
    /// </summary>
    public async Task ResumeRunAsync(AssistantRunJournal journal)
    {
        if (_app.Documents is not { } documents || _activeRuns.ContainsKey(journal.ConversationId)) return;
        var uncertain = journal.Effects(documents).Where(e => e.State == AiRunEffect.Uncertain).ToList();
        if (uncertain.Count > 0 && !await _app.Dialogs.ConfirmAsync("Confira antes de retomar",
                "O app foi encerrado no meio destas ações e o resultado não pôde ser verificado:\n\n" + string.Join("\n", uncertain.Select(u => "• " + u.Detail)) +
                "\n\nO agente não vai repeti-las sem perguntar a você.", "Retomar"))
            return;
        var conv = RebuiltConversation(journal, documents);
        var note = journal.ResumeNote(documents);
        conv.Messages.Add(note);
        try { _store.SaveConversationVerified(conv); }
        catch (IOException e) { _app.Notify(e.Message); return; }
        journal.Resume(note);
        InterruptedChanged();
        _liveRuns.Add(journal.RunId);
        OpenConversation(conv);
        CurrentRun = StartAsync(conv, resume: journal);
        if (_activeRuns.ContainsKey(conv.Id)) _activeTasks[conv.Id] = CurrentRun;
        await CurrentRun;
    }
}
