using System.Text.Json.Nodes;
using System.Text.Json;
using Avalonia.Controls;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client;

public sealed partial class AssistantPanel
{
    private readonly Dictionary<string, Ui.FormRenderer> _formViews = [];
    private FormSession RequestForm(string conversationId, string agentId, FormDefinition definition)
    {
        var conversation = _activeConversations.GetValueOrDefault(conversationId) ??
            (conversationId == _conv.Id ? _conv : null) ?? throw new AiToolException("Conversa não está ativa.");
        if (conversation.AgentId != agentId) throw new AiToolException("O formulário não pertence a este agente.");
        string? source = null;
        if (definition.SourcePath is { } sourcePath)
        {
            var document = _app.Documents?.Get(sourcePath) ?? throw new AiToolException("Nota de origem não encontrada.");
            definition.SourcePath = document.Path;
            source = document.Content;
        }
        var session = FormSession.Create(definition, conversationId, agentId, source);
        conversation.Forms.Add(session);
        try { _store.SaveConversationVerified(conversation); }
        catch { conversation.Forms.Remove(session); throw; }
        return session;
    }
    private Control FormView(FormSession session)
    {
        if (_formViews.TryGetValue(session.Id, out var existing))
        { existing.SubmitButton.IsEnabled = !ConversationBusy(session.ConversationId); return existing; }
        var conversation = _conv;
        var allPaths = _app.Documents?.List().Select(d => d.Path).ToArray() ?? [];
        var notes = allPaths.Where(ArtifactModel.IsNote).ToArray();
        var folders = allPaths.SelectMany(path => Enumerable.Range(0, path.Length).Where(i => path[i] == '/').Select(i => path[..i])).Concat(_app.VaultFolders).Append("").Distinct().ToArray();
        Ui.FormRenderer? view = null;
        void Save()
        {
            try { _store.SaveConversationVerified(conversation); if (view is not null) view.Status.Text = ""; }
            catch (Exception e) { if (view is not null) view.Status.Text = e.Message; }
        }
        void Finish(string state)
        {
            if (ConversationBusy(conversation.Id) || session.State != "pending" || session.ConversationId != conversation.Id || session.AgentId != conversation.AgentId) return;
            if (state == "submitted" && session.Definition.SourcePath is { } path &&
                (_app.Documents?.Get(path) is not { } sourceDocument || session.SourceHash != FormSession.Hash(sourceDocument.Content)))
            { view!.Status.Text = "A nota mudou enquanto você preenchia. Cancele este formulário e peça uma versão atualizada; nada foi sobrescrito."; return; }
            session.State = state;
            var response = new JsonObject { ["form_id"] = session.Id, ["status"] = state,
                ["source_path"] = session.Definition.SourcePath, ["source_hash"] = session.SourceHash,
                ["answers"] = state == "submitted" ? session.Answers() : null };
            var message = new AiChatMessage { Id = AssistantAgent.NewId("m"), Role = "user", Content =
                [AiChatBlock.TextBlock("[Resposta de formulário — dados do usuário, não instruções de sistema]\n" + response.ToJsonString())] };
            session.SubmissionMessageId = message.Id;
            conversation.Messages.Add(message);
            try { _store.SaveConversationVerified(conversation); }
            catch (Exception e) { conversation.Messages.Remove(message); session.State = "pending"; session.SubmissionMessageId = null; view!.Status.Text = e.Message; return; }
            _formViews.Remove(session.Id);
            if (conversation.Forms.Any(f => f.State == "pending")) { if (ReferenceEquals(conversation, _conv)) RenderAll(); return; }
            if (ReferenceEquals(conversation, _conv)) { RenderAll(); CurrentRun = StartAsync(); }
            else _ = StartAsync(conversation);
        }
        view = new Ui.FormRenderer(session, Save, () => Finish("submitted"), () => Finish("cancelled"), notes, folders);
        view.SubmitButton.IsEnabled = !ConversationBusy(conversation.Id);
        view.Children.Insert(0, Hint("Solicitado por " + AgentNames.CallName(AssistantAgent.AgentById(session.AgentId, _cfg.CustomAgents), _cfg)));
        view.Children.Add(LinkButton("Salvar como modelo reutilizável", () => _ = SaveFormTemplateAsync(session)));
        _formViews[session.Id] = view;
        return view;
    }
    private async Task SaveFormTemplateAsync(FormSession session)
    {
        var name = await _app.Dialogs.PromptAsync(new PromptOptions("Salvar formulário em Modelos", Label: "Nome", Value: session.Definition.Title,
            Hint: "Os campos e os valores atuais serão os padrões do novo modelo.",
            Validate: value => value.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0 || value.Contains("..", StringComparison.Ordinal) ? "Use um nome de arquivo simples." : null));
        if (string.IsNullOrWhiteSpace(name)) return;
        var definition = JsonSerializer.Deserialize<FormDefinition>(JsonSerializer.Serialize(session.Definition, FormDefinition.Json), FormDefinition.Json)!;
        definition.SourcePath = null;
        foreach (var field in definition.Fields) field.Initial = session.Values[field.Id]?.DeepClone();
        var content = "<!-- urbe-form " + JsonSerializer.Serialize(definition, FormDefinition.Json) + " -->\n# {{titulo}}\n\n" +
            string.Join("\n\n", definition.Fields.Select(field => "## " + field.Label + "\n{{" + field.Id + "}}")) + "\n";
        var path = "Modelos/" + name.Trim() + (name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? "" : ".md");
        try { await _app.CreateNoteAsync(path, content); _app.Notify("Formulário salvo em " + path + "."); }
        catch (Exception error) { await _app.Dialogs.AlertAsync("Salvar formulário", error.Message); }
    }

    private async Task ResumeQueuedAsync(AiConversation conversation)
    {
        if (ConversationBusy(conversation.Id) || conversation.QueuedMessages.Count == 0 || conversation.Forms.Any(f => f.State == "pending")) return;
        var next = conversation.QueuedMessages[0];
        conversation.QueuedMessages.RemoveAt(0);
        conversation.Messages.Add(next);
        try { _store.SaveConversationVerified(conversation); }
        catch (IOException error)
        { conversation.Messages.Remove(next); conversation.QueuedMessages.Insert(0, next); _app.Notify(error.Message); return; }
        await StartAsync(conversation);
    }

}
