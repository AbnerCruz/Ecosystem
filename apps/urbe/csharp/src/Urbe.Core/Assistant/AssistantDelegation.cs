using System.Text.Json.Nodes;

namespace Urbe.Core.Assistant;

/// <summary>
/// CST-217: what a specialist is given when work is delegated to it — a scoped brief, never the asking agent's
/// conversation or memory: the task, the notes it should consult, where it may write, the artifact expected, and its
/// turn budget. A reviewer gets the artifact to inspect (path and revision) and cannot write anything.
/// </summary>
public sealed record AiDelegationBrief(string Task, IReadOnlyList<string> Scope, IReadOnlyList<string> Sources, string Expected,
    string? ArtifactPath, int MaxSteps, string Role = AiDelegationBrief.Delegate, string? ReviewSha = null)
{
    public const string Delegate = "delegate";
    public const string Reviewer = "reviewer";
    /// <summary>The turn budget when the caller names none: room for real work, not a hidden cap.</summary>
    public const int DefaultSteps = 40;
    /// <summary>The most a caller may give; past it the work is too big to delegate as one piece and should be split.</summary>
    public const int MostSteps = 80;

    public bool IsReviewer => Role == Reviewer;

    /// <summary>Whether the brief lets the specialist write <paramref name="path"/> (a file or a folder of the scope).</summary>
    public bool Allows(string? path)
    {
        var p = AssistantTools.Norm(path);
        if (p.Length == 0 || IsReviewer) return false;
        static string Bare(string s) => s.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? s[..^3] : s;
        foreach (var entry in Scope)
        {
            var e = AssistantTools.Norm(entry);
            if (e.Length == 0) continue;
            if (string.Equals(Bare(p), Bare(e), StringComparison.OrdinalIgnoreCase)) return true;
            if (p.StartsWith(e + "/", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>The message the specialist receives: structured, with nothing of the asking conversation.</summary>
    public string Message(string requester)
    {
        var lines = new List<string>
        {
            IsReviewer ? "[Revisão independente pedida por " + requester + "]" : "[Trabalho delegado por " + requester + "]",
            "Tarefa: " + Task
        };
        if (Sources.Count > 0) lines.Add("Fontes indicadas (leia-as no vault): " + string.Join(", ", Sources));
        if (IsReviewer)
        {
            lines.Add("Artefato a revisar: " + ArtifactPath + (ReviewSha is null ? "" : " (revisão " + ReviewSha[..Math.Min(10, ReviewSha.Length)] + ")") + ".");
            lines.Add("Leia o arquivo real com read_note antes de opinar; um resumo de quem pediu não é o artefato. Você não pode alterar nada.");
            lines.Add("Responda com: PARECER: aprovado | ajustes necessários; MOTIVOS: o que viu no arquivo, citando trechos; PENDÊNCIAS.");
        }
        else
        {
            lines.Add(Scope.Count > 0 ? "Escopo de escrita: " + string.Join(", ", Scope) + ". Gravações fora dele são recusadas pelo Urbe."
                : "Escopo de escrita: nenhum. Não altere o vault; devolva o resultado em texto.");
            lines.Add(Expected == "note" && ArtifactPath is not null
                ? "Resultado esperado: a nota " + ArtifactPath + ", gravada no vault."
                : "Resultado esperado: texto na sua resposta.");
            lines.Add("Ao terminar, responda com: ARTEFATO: o caminho da nota (ou \"texto\"); JUSTIFICATIVA: como chegou ao resultado e quais fontes usou; PENDÊNCIAS: o que não foi possível fazer.");
        }
        lines.Add("Limite deste trabalho: " + MaxSteps + " etapas. Você não vê a conversa nem as memórias de quem pediu; peça o que faltar.");
        return string.Join("\n", lines);
    }

    public JsonObject ToJson()
    {
        var o = new JsonObject
        {
            ["task"] = Task, ["scope"] = new JsonArray(Scope.Select(s => (JsonNode?)s).ToArray()),
            ["sources"] = new JsonArray(Sources.Select(s => (JsonNode?)s).ToArray()), ["expected"] = Expected,
            ["artifact"] = ArtifactPath, ["maxSteps"] = MaxSteps, ["role"] = Role
        };
        if (ReviewSha is not null) o["reviewSha"] = ReviewSha;
        return o;
    }

    public static AiDelegationBrief? FromJson(JsonNode? n) => n is not JsonObject o ? null : new(
        Str(o["task"]), (o["scope"] as JsonArray ?? []).Select(Str).Where(s => s.Length > 0).ToList(),
        (o["sources"] as JsonArray ?? []).Select(Str).Where(s => s.Length > 0).ToList(), Str(o["expected"]) is { Length: > 0 } e ? e : "text",
        o["artifact"] is null ? null : Str(o["artifact"]), Math.Clamp(o["maxSteps"] is JsonValue v && v.TryGetValue<int>(out var steps) ? steps : DefaultSteps, 1, MostSteps),
        Str(o["role"]) == Reviewer ? Reviewer : Delegate, o["reviewSha"] is null ? null : Str(o["reviewSha"]));

    private static string Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
}

/// <summary>What came back from a specialist: its answer, what it changed and the steps it took (its transcript).</summary>
public sealed record AiDelegationOutcome(string ConversationId, string AgentName, string Answer, IReadOnlyList<AiChange> Changes,
    string? Stopped, IReadOnlyList<AiChatMessage> Transcript);

/// <summary>
/// CST-217: the host a delegated specialist works through. Everything is the specialist's own (its memory, its
/// conversations, its permissions), and writes are allowed only inside the brief's scope: a note, a file, a graph or
/// an image outside it is refused before anything is written. Global changes (appearance, plugins, the agent's name),
/// further delegation and the asking conversation's attachments are not reachable from a delegated work.
/// </summary>
public sealed class AssistantScopedHost(IAssistantHost inner, AiDelegationBrief brief) : IAssistantHost
{
    public AiDelegationBrief Brief => brief;

    private void Check(string? path)
    {
        if (brief.Allows(path)) return;
        throw new AiToolException(brief.IsReviewer
            ? "Revisões não alteram o vault: nada foi gravado. Descreva os ajustes no parecer."
            : "Fora do escopo deste trabalho delegado: " + AssistantTools.Norm(path) + ". Nada foi gravado. Escopo permitido: " +
              (brief.Scope.Count > 0 ? string.Join(", ", brief.Scope) : "nenhum (devolva o resultado em texto)") + ".");
    }

    public DocumentStore Documents => inner.Documents;
    public KnowledgeIndex Knowledge => inner.Knowledge;
    public (string? Path, string? Selection) EditorContext() => inner.EditorContext();
    public int ReadChars => inner.ReadChars;
    public string Readable(string text) => inner.Readable(text);
    public Task<UrbeDocument> CreateNoteAsync(string path, string content) { Check(path); return inner.CreateNoteAsync(path, content); }
    public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string content) { Check(doc.Path); return inner.SaveNoteAsync(doc, content); }
    public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string newPath) { Check(doc.Path); Check(newPath); return inner.RenameNoteAsync(doc, newPath); }
    public Task TrashNoteAsync(UrbeDocument doc) { Check(doc.Path); return inner.TrashNoteAsync(doc); }
    public Task CreateFolderAsync(string path) { Check(path); return inner.CreateFolderAsync(path); }
    public void OpenNote(string path) => inner.OpenNote(path);
    public void Remember(string fact) => inner.Remember(fact);
    public IReadOnlyList<AiMemoryMatch> SearchMemory(string query, int limit) => inner.SearchMemory(query, limit);
    public AiMemoryRecord StoreMemory(string fact, string kind, string scope, string sourcePath) => inner.StoreMemory(fact, kind, scope, sourcePath);
    public IReadOnlyList<(AiMemoryRecord Left, AiMemoryRecord Right)> MemoryConflicts() => inner.MemoryConflicts();
    public bool SupersedeMemory(string oldId, string newId) => inner.SupersedeMemory(oldId, newId);
    public bool ForgetMemory(string id) => inner.ForgetMemory(id);
    public void SetPlan(IReadOnlyList<AiPlanStep> steps) => inner.SetPlan(steps);
    public IAssistantAppearance? Appearance => null;
    public IAssistantPlugins? Plugins => null;
    public IAssistantNames? Names => null;
    public IAssistantImages? Images => inner.Images is { } images ? new ScopedImages(images, this) : null;
    public AssistantWebSession? Web => inner.Web;
    public IAssistantGraphs? Graphs => inner.Graphs is { } graphs ? new ScopedGraphs(graphs, this) : null;
    /// <summary>Depth 1: a delegated specialist does not delegate again.</summary>
    public IAssistantDelegation? Delegation => null;
    public IAssistantConversations? Conversations => inner.Conversations;
    public IAssistantForms? Forms => inner.Forms;
    public IAssistantAttachmentExtractor? Attachments => null;
    public IAssistantDocumentFiles? DocumentFiles => inner.DocumentFiles;
    public Task<string?> ReadDataAsync(string path) => inner.ReadDataAsync(path);
    public IAssistantDocumentVision? DocumentVision => inner.DocumentVision;

    private sealed class ScopedImages(IAssistantImages images, AssistantScopedHost host) : IAssistantImages
    {
        public bool CanDraw => images.CanDraw;
        public string Unavailable => images.Unavailable;
        public Task<byte[]> DrawAsync(string prompt, string size, CancellationToken cancel) => images.DrawAsync(prompt, size, cancel);
        public Task SaveAsync(string path, byte[] bytes) { host.Check(path); return images.SaveAsync(path, bytes); }
        public bool Exists(string path) => images.Exists(path);
    }

    private sealed class ScopedGraphs(IAssistantGraphs graphs, AssistantScopedHost host) : IAssistantGraphs
    {
        public IReadOnlyList<string> ListPaths() => graphs.ListPaths();
        public Task<string?> ReadAsync(string path) => graphs.ReadAsync(path);
        public Task WriteAsync(string path, string json, bool create) { host.Check(path); return graphs.WriteAsync(path, json, create); }
    }
}
