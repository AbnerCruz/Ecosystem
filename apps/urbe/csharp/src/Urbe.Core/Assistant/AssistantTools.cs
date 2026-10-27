using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core.Assistant;

/// <summary>A note before or after a change: its path and text.</summary>
public sealed record AiNoteSnap(string Path, string Content);

/// <summary>A change a tool made, kept so the run can be undone as a block (before/after).</summary>
public sealed record AiChange(string Id, AiNoteSnap? Before, AiNoteSnap? After, bool Trashed = false);

/// <summary>A note read only in part: what is missing.</summary>
public sealed record AiPartial(string Path, int Next, int Total);

/// <summary>A step of the agent's plan.</summary>
public sealed record AiPlanStep(string Step, string Status, string? Details = null, string? Acceptance = null);

/// <summary>What a tool returns: the text for the model and what it changed or read.</summary>
public sealed record AiToolOutput(string Content, IReadOnlyList<AiChange>? Changes = null, IReadOnlyList<(string Path, AiPartial? Partial)>? Reads = null);

/// <summary>What the approval shows: the note and its text before and after (or a note of what happens).</summary>
public sealed record AiPreview(string Path, string? Before, string? After, string? Note = null);

public static partial class AssistantTools
{
    /// <summary>
    /// UC-51: every text the Assistente writes into a note ends with its mark, <c>{ia:"modelo"}</c> (NoteMarks.AiMark), so
    /// it can always be told apart; the person sees it in the preview and can edit it before approving.
    /// </summary>
    public static void Watermark(AiTool tool, JsonObject input, string? model)
    {
        var field = tool.Name switch { "create_note" or "write_note" or "append_to_note" => "content", "edit_note" => "new_text", _ => null };
        if (field is null || input[field] is not JsonValue v || !v.TryGetValue<string>(out var text) || text.Trim().Length == 0) return;
        input[field] = NoteMarks.WithAiMark(text, model ?? "IA");
    }

    /// <summary>Tools whose preview's "after" is the note's whole new text, so the person can edit it before approving.</summary>
    public static bool CanEdit(AiTool tool, AiPreview? preview) =>
        preview?.After is not null && tool.Name is "create_note" or "write_note" or "edit_note" or "append_to_note";

    /// <summary>The call that writes <paramref name="edited"/> instead of what the model proposed: create_note keeps
    /// creating; the others become write_note of the same note with the person's text.</summary>
    public static (AiTool Tool, JsonObject Input)? EditedRun(AiTool tool, JsonObject input, AiPreview? preview, string edited)
    {
        if (!CanEdit(tool, preview)) return null;
        if (tool.Name == "create_note") return (tool, new JsonObject { ["path"] = input["path"]?.DeepClone(), ["content"] = edited });
        return (Get("write_note")!, new JsonObject { ["path"] = input["path"]?.DeepClone(), ["content"] = edited });
    }
}

/// <summary>A tool's failure the model can act on (ToolError: the message goes as is).</summary>
public sealed class AiToolException(string message) : Exception(message);

/// <summary>
/// What the tools need from the app. Reads come from the document store and the index (the source of truth); every
/// write goes through the app, which changes the vault first and only then the store and the notes' record.
/// </summary>
public interface IAssistantHost
{
    DocumentStore Documents { get; }
    KnowledgeIndex Knowledge { get; }
    /// <summary>The note open in the editor and the text selected in it.</summary>
    (string? Path, string? Selection) EditorContext();
    /// <summary>How much of a note one read may bring (proportional to the model's context).</summary>
    int ReadChars { get; }
    Task<UrbeDocument> CreateNoteAsync(string path, string content);
    Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string content);
    /// <summary>Moves or renames a note (the file first); the other notes' links are the tool's.</summary>
    Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string newPath);
    Task TrashNoteAsync(UrbeDocument doc);
    Task CreateFolderAsync(string path);
    void OpenNote(string path);
    void Remember(string fact);
    IReadOnlyList<AiMemoryMatch> SearchMemory(string query, int limit) => [];
    AiMemoryRecord StoreMemory(string fact, string kind, string scope, string sourcePath) =>
        throw new AiToolException("Memória estruturada indisponível neste host.");
    IReadOnlyList<(AiMemoryRecord Left, AiMemoryRecord Right)> MemoryConflicts() => [];
    bool SupersedeMemory(string oldId, string newId) => false;
    bool ForgetMemory(string id) => false;
    void SetPlan(IReadOnlyList<AiPlanStep> steps);
    /// <summary>The Personalização the appearance tools change, when the app has one.</summary>
    IAssistantAppearance? Appearance => null;
    /// <summary>The vault's plugins the plugin tools create and turn on, when the app has them.</summary>
    IAssistantPlugins? Plugins => null;
    /// <summary>UC-81: the call name of the run's agent, when the app keeps them.</summary>
    IAssistantNames? Names => null;
    /// <summary>UC-63: drawing pictures with the run's provider and keeping them in the vault, when the app can.</summary>
    IAssistantImages? Images => null;
    /// <summary>Explicitly authorized network gateway; null keeps every local feature offline.</summary>
    AssistantWebSession? Web => null;
    /// <summary>Explicitly read-only view of authored graph documents in this vault.</summary>
    IAssistantGraphs? Graphs => null;
    /// <summary>Agent-to-agent requests transmit only the explicit task, never another chat or memory.</summary>
    IAssistantDelegation? Delegation => null;
    IAssistantForms? Forms => null;
    /// <summary>Only the current conversation may supply a received user attachment for extraction.</summary>
    IAssistantAttachmentExtractor? Attachments => this as IAssistantAttachmentExtractor;
}

/// <summary>A configured public agent profile, without private memory or chat contents.</summary>
public sealed record AiDelegateTarget(string Id, string Name, string Description, bool CanGenerateImages);

public interface IAssistantDelegation
{
    IReadOnlyList<AiDelegateTarget> Targets { get; }
    Task<string> ConsultAsync(string agentId, string task, string? conversationId = null);
}

/// <summary>A tool the agents use (tools.js def): name, description, JSON Schema, access, label, preview and run.</summary>
public sealed class AiTool
{
    public required string Name { get; init; }
    /// <summary>'read' | 'write' | 'destructive' | 'ui' | 'memory' | 'plan'.</summary>
    public required string Access { get; init; }
    public required string Description { get; init; }
    public required JsonObject Parameters { get; init; }
    public required Func<JsonObject, string> Label { get; init; }
    public Func<JsonObject, IAssistantHost, AiPreview>? Preview { get; init; }
    public required Func<JsonObject, IAssistantHost, Task<AiToolOutput>> Run { get; init; }
    public AiToolSchema Schema => new(Name, Description, (JsonObject)Parameters.DeepClone());
}

/// <summary>
/// The tools of the 1.8.4's assistant (src/ai/tools.js), ported: reading (overview, list, search, read, grep, links,
/// tags, editor), writing (create, edit, rewrite, append, rename with links, trash, folder) and interface/memory
/// (open, plan, remember), with the original's descriptions, schemas, labels and messages, and validate().
/// </summary>
public static partial class AssistantTools
{
    private const int Limit = 24000;
    private static readonly Regex NoteRe = new(@"\.(md|markdown)$", RegexOptions.IgnoreCase);
    private static readonly Regex TextRe = new(@"\.(md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv)$", RegexOptions.IgnoreCase);

    // ---------------------------------------------------------------- helpers

    /// <summary>norm(p): trimmed, forward slashes, no leading/trailing or doubled slashes.</summary>
    public static string Norm(string? p) =>
        Regex.Replace(Regex.Replace(LegacyCustomize.JsTrim(p ?? "").Replace('\\', '/'), "^/+|/+$", ""), "/+", "/");

    /// <summary>fold(s): accents dropped, lower case.</summary>
    public static string Fold(string? s)
    {
        var d = (s ?? "").Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d) if (c < '̀' || c > 'ͯ') sb.Append(c);
        return JsText.Lower(sb.ToString());
    }

    private static string Loose(string s) => Regex.Replace(Fold(s), @"[^\p{L}\p{N}/]+", "");

    private static int Words(string? t) => Regex.Matches(t ?? "", @"\S+").Count;

    /// <summary>clip(s, n).</summary>
    public static string Clip(string s, int n = Limit) => s.Length > n ? s[..n] + "\n…[cortado: +" + (s.Length - n) + " caracteres; peça um trecho com start_line]" : s;

    private static string Str(JsonObject i, string k) => i[k] is { } v ? LegacyCustomize.JsToString(v) : "";
    private static bool Has(JsonObject i, string k) => i[k] is { } v && !(v is JsonValue jv && ((jv.TryGetValue<string>(out var s) && s.Length == 0) || (jv.TryGetValue<bool>(out var b) && !b)));
    private static int? Int(JsonObject i, string k) => i[k] is { } v && double.IsFinite(LegacyCustomize.JsToNumber(v)) ? (int)LegacyCustomize.JsToNumber(v) : null;

    private static string Today() => DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static AiNoteSnap? Snap(UrbeDocument? d) => d is null ? null : new AiNoteSnap(d.Path, d.Content);

    private static int Score(string a, string b)
    {
        if (a == b) return 100;
        if (a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal)) return 50;
        int n = 0;
        foreach (var w in Regex.Split(b, @"[\s/._-]+")) if (w.Length > 2 && a.Contains(w, StringComparison.Ordinal)) n += 10;
        return n;
    }

    /// <summary>resolveDoc(ctx, ref, must): path, path without .md, exact or accent-free title; suggests similar ones.</summary>
    public static UrbeDocument? ResolveDoc(IAssistantHost host, string? reference, bool must = true)
    {
        var docs = host.Documents;
        var p = Norm(reference);
        if (p.Length == 0) throw new AiToolException("Informe o caminho da nota.");
        var all = docs.List();
        var d = docs.Get(p) ?? docs.Get(p + ".md");
        if (d is null)
        {
            var t = Fold(NoteRe.Replace(p, "").Split('/')[^1]);
            var hits = all.Where(x => Fold(x.Title) == t).ToList();
            if (hits.Count == 1) d = hits[0];
            else if (hits.Count > 1 && must)
                throw new AiToolException("Há " + hits.Count + " notas chamadas \"" + p + "\": " + string.Join(", ", hits.Select(x => x.Path)) + ". Use o caminho completo.");
        }
        if (d is null)
        {
            string Base(string x) => NoteRe.Replace(x, "");
            var lk = Loose(Base(p));
            var lt = Loose(Base(p).Split('/')[^1]);
            if (lk.Length > 0)
            {
                var lh = all.Where(x => Loose(Base(x.Path)) == lk).ToList();
                if (lh.Count == 0 && lt.Length > 0) lh = all.Where(x => Loose(x.Title) == lt || Loose(Base(x.Path).Split('/')[^1]) == lt).ToList();
                if (lh.Count == 1) d = lh[0];
            }
        }
        if (d is null && must)
        {
            var near = all.Select(x => (x, s: Score(Fold(x.Path), Fold(p)))).Where(o => o.s > 0).OrderByDescending(o => o.s).Take(5).Select(o => o.x.Path).ToList();
            throw new AiToolException("Nota não encontrada: " + p + (near.Count > 0 ? ". Parecidas: " + string.Join(", ", near) : ". Use search_notes ou list_notes para achar o caminho."));
        }
        return d;
    }

    /// <summary>validPath(p): no dot folders or hidden files, no forbidden characters; .md when it has no text extension.</summary>
    public static string ValidPath(string? p0)
    {
        var p = Norm(p0);
        if (p.Length == 0) throw new AiToolException("Caminho vazio.");
        if (Regex.IsMatch(p, @"(^|/)\.\.?(/|$)") || p.Split('/')[^1].StartsWith('.')) throw new AiToolException("Caminho inválido: " + p);
        if (Regex.IsMatch(p, "[:*?\"<>|\u0000-\u001f]")) throw new AiToolException("Caminho com caracteres não permitidos: " + p);
        if (!TextRe.IsMatch(p)) p += ".md";
        return p;
    }

    private static int ReadBudget(IAssistantHost host) => Math.Max(8000, host.ReadChars > 0 ? host.ReadChars : 24000);

    /// <summary>readSlice(d, start, maxLines, budget): the note with line numbers, whole when it fits.</summary>
    public static (string Text, AiPartial? Partial) ReadSlice(UrbeDocument d, int? start, int? maxLines, int budget, bool exactRange = false)
    {
        var lines = d.Content.Split('\n');
        int total = lines.Length;
        int s = Math.Min(Math.Max(1, start is > 0 ? start.Value : 1), total);
        int rest = string.Join("\n", lines.Skip(s - 1)).Length;
        int requestedEnd = exactRange && maxLines is > 0 ? (int)Math.Min(total, (long)s - 1 + maxLines.Value) : total;
        int e = s - 1, used = 0;
        while (e < requestedEnd && (used + lines[e].Length + 8 <= budget || e == s - 1)) { used += lines[e].Length + 8; e++; }
        if (rest > budget && maxLines is > 0) e = Math.Min(e, s - 1 + maxLines.Value);
        int w = e.ToString(CultureInfo.InvariantCulture).Length;
        var output = new List<string>();
        for (int i = s; i <= e; i++) output.Add(i.ToString(CultureInfo.InvariantCulture).PadLeft(w, ' ') + "| " + lines[i - 1]);
        var head = "# " + d.Path + " (" + total + " linhas" + (d.Tags.Count > 0 ? " · tags: " + string.Join(" ", d.Tags.Select(t => "#" + t)) : "") + ")" +
                   (s > 1 || e < total ? " — linhas " + s + "–" + e : " — completa");
        var body = d.Content.Length > 0 ? string.Join("\n", output) : "(nota vazia)";
        AiPartial? partial = e < requestedEnd ? new AiPartial(d.Path, e + 1, requestedEnd) : null;
        if (partial is not null)
            body += "\n\n⚠ LEITURA PARCIAL: faltam as linhas " + (e + 1) + "–" + requestedEnd + ". Antes de responder sobre esta nota, chame read_note com path=\"" + d.Path + "\" e start_line=" + (e + 1) + ".";
        return (head + "\n" + body, partial);
    }

    /// <summary>applyEdit(content, old, new, all).</summary>
    public static string ApplyEdit(string content, string oldText, string newText, bool all)
    {
        if (oldText.Length == 0) throw new AiToolException("old_text vazio. Para trocar a nota inteira use write_note; para acrescentar, append_to_note.");
        int n = CountOf(content, oldText);
        if (n == 0)
        {
            var l = LegacyCustomize.JsTrim(Regex.Replace(Fold(oldText), @"\s+", " "));
            var c = Regex.Replace(Fold(content), @"\s+", " ");
            throw new AiToolException(c.Contains(l, StringComparison.Ordinal)
                ? "old_text existe mas com espaços/quebras de linha diferentes. Leia a nota de novo (read_note) e copie o trecho exatamente."
                : "old_text não aparece na nota. Leia a nota de novo (read_note) e copie o trecho exatamente.");
        }
        if (n > 1 && !all) throw new AiToolException("old_text aparece " + n + " vezes. Inclua mais contexto para ficar único ou use replace_all.");
        if (all) return content.Replace(oldText, newText, StringComparison.Ordinal);
        int at = content.IndexOf(oldText, StringComparison.Ordinal);
        return content[..at] + newText + content[(at + oldText.Length)..];
    }

    private static int CountOf(string s, string part)
    {
        int n = 0;
        for (int i = s.IndexOf(part, StringComparison.Ordinal); i >= 0; i = s.IndexOf(part, i + part.Length, StringComparison.Ordinal)) n++;
        return n;
    }

    private static readonly Regex HeadingRe = new(@"^(#{1,6})\s+(.*)$");

    /// <summary>appendText(content, text, heading): at the end, or at the end of a section.</summary>
    public static string AppendText(string content, string text, string? heading)
    {
        if (string.IsNullOrEmpty(heading)) return Regex.Replace(content, @"\s*$", "") + (LegacyCustomize.JsTrim(content).Length > 0 ? "\n\n" : "") + text + "\n";
        var lines = content.Split('\n').ToList();
        var h = Fold(Regex.Replace(heading, @"^#+\s*", ""));
        int at = -1, level = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            var m = HeadingRe.Match(lines[i]);
            if (m.Success && LegacyCustomize.JsTrim(Fold(m.Groups[2].Value)) == h) { at = i; level = m.Groups[1].Length; break; }
        }
        if (at < 0)
        {
            var titles = lines.Where(l => Regex.IsMatch(l, @"^#{1,6}\s")).ToList();
            throw new AiToolException("Seção \"" + heading + "\" não encontrada. Títulos existentes: " + (titles.Count > 0 ? string.Join(" | ", titles) : "nenhum"));
        }
        int end = lines.Count;
        for (int j = at + 1; j < lines.Count; j++)
        {
            var mm = Regex.Match(lines[j], @"^(#{1,6})\s");
            if (mm.Success && mm.Groups[1].Length <= level) { end = j; break; }
        }
        while (end > at + 1 && LegacyCustomize.JsTrim(lines[end - 1]).Length == 0) end--;
        lines.Insert(end, text);
        return string.Join("\n", lines);
    }

    private static JsonObject Schema(string json) => (JsonObject)JsonNode.Parse(json)!;

    // ---------------------------------------------------------------- the tools

    public static IReadOnlyList<AiTool> All { get; } = Make();

    public static AiTool? Get(string name) => All.FirstOrDefault(t => t.Name == name);

    private static List<AiTool> Make() =>
    [
        new()
        {
            Name = "workspace_overview", Access = "read",
            Description = "Visão geral do vault: número de notas, pastas, notas recentes, tags mais usadas e a nota aberta no editor. Use no começo de tarefas amplas.",
            Parameters = Schema("""{"type":"object","properties":{},"additionalProperties":false}"""),
            Label = _ => "Olhando o vault",
            Run = (_, host) =>
            {
                var all = host.Documents.List();
                var folders = new SortedDictionary<string, int>(StringComparer.Ordinal);
                var tags = new Dictionary<string, int>(StringComparer.Ordinal);
                var tagOrder = new List<string>();
                foreach (var d in all)
                {
                    var f = d.Path.Contains('/') ? d.Path[..d.Path.LastIndexOf('/')] : "(raiz)";
                    folders[f] = folders.GetValueOrDefault(f) + 1;
                    foreach (var t in d.Tags) { if (!tags.ContainsKey(t)) tagOrder.Add(t); tags[t] = tags.GetValueOrDefault(t) + 1; }
                }
                var recent = all.OrderBy(d => d.Modified ?? "", Comparer<string>.Create((a, b) => JsText.LocaleCompare(b, a))).Take(10).Select(d => "- " + d.Path);
                var open = host.EditorContext().Path;
                var topTags = tagOrder.OrderByDescending(t => tags[t]).Take(20).Select(t => "#" + t + " (" + tags[t] + ")").ToList();
                return Task.FromResult(new AiToolOutput(string.Join("\n",
                    "Notas: " + all.Count + " · ligações: " + host.Knowledge.Stats().Links + " · pastas: " + folders.Count,
                    "Nota aberta: " + (string.IsNullOrEmpty(open) ? "nenhuma" : open),
                    "\nPastas (notas):\n" + string.Join("\n", folders.Select(f => "- " + f.Key + " (" + f.Value + ")")),
                    "\nRecentes:\n" + string.Join("\n", recent),
                    "\nTags: " + (topTags.Count > 0 ? string.Join(", ", topTags) : "nenhuma"))));
            }
        },
        new()
        {
            Name = "list_notes", Access = "read",
            Description = "Lista notas de uma pasta (ou do vault inteiro), com tamanho e data. Pastas são o começo do caminho, ex.: \"Projetos\".",
            Parameters = Schema("""{"type":"object","properties":{"folder":{"type":"string","description":"Pasta; vazio = vault inteiro"},"recursive":{"type":"boolean","description":"Incluir subpastas (padrão true)"},"limit":{"type":"integer","minimum":1,"maximum":500}},"additionalProperties":false}"""),
            Label = i => "Listando " + (Has(i, "folder") ? "“" + Str(i, "folder") + "”" : "o vault"),
            Run = (i, host) =>
            {
                var f = Norm(Str(i, "folder"));
                bool rec = !(i["recursive"] is JsonValue rv && rv.TryGetValue<bool>(out var r) && !r);
                int lim = Int(i, "limit") is { } l && l != 0 ? l : 150;
                var hits = host.Documents.List().Where(d =>
                {
                    if (f.Length == 0) return rec || !d.Path.Contains('/');
                    if (!d.Path.ToLowerInvariant().StartsWith(f.ToLowerInvariant() + "/", StringComparison.Ordinal)) return false;
                    return rec || !d.Path[(f.Length + 1)..].Contains('/');
                }).OrderBy(d => d.Path, Comparer<string>.Create(JsText.LocaleCompare)).ToList();
                if (hits.Count == 0) return Task.FromResult(new AiToolOutput(f.Length > 0 ? "Nenhuma nota em \"" + f + "\"." : "O vault está vazio."));
                return Task.FromResult(new AiToolOutput(string.Join("\n", hits.Take(lim).Select(d => "- " + d.Path + " · " + Words(d.Content) + " palavras" + (string.IsNullOrEmpty(d.Modified) ? "" : " · " + d.Modified))) +
                                                        (hits.Count > lim ? "\n… e mais " + (hits.Count - lim) : "")));
            }
        },
        new()
        {
            Name = "search_notes", Access = "read",
            Description = "Busca notas por título e conteúdo (ignora acentos, aceita partes de palavras). Devolve caminho e trecho. Use antes de criar notas, para não duplicar.",
            Parameters = Schema("""{"type":"object","properties":{"query":{"type":"string"},"limit":{"type":"integer","minimum":1,"maximum":50}},"required":["query"],"additionalProperties":false}"""),
            Label = i => "Buscando “" + Str(i, "query") + "”",
            Run = (i, host) =>
            {
                var q = LegacyCustomize.JsTrim(Str(i, "query"));
                if (q.Length == 0) throw new AiToolException("Busca vazia.");
                var docs = host.Documents.List().Select(d => new LegacySearchDoc(d.Id, d.Title, d.Path, d.Content, d.Modified)).ToList();
                var hits = LegacyQuickOpen.Rank(docs, q, Int(i, "limit") is { } l && l != 0 ? l : 12).Select(r => "- " + r.Doc.Path +
                    (r.Snippet is { } sn ? "\n  …" + Slice160(LegacyCustomize.JsTrim(Regex.Replace(sn.Before + sn.Match + sn.After, @"\s+", " "))) : "")).ToList();
                return Task.FromResult(new AiToolOutput(hits.Count > 0 ? string.Join("\n", hits) : "Nada encontrado para \"" + q + "\"."));
            }
        },
        new()
        {
            Name = "read_note", Access = "read",
            Description = "Lê uma nota inteira, com números de linha. Notas muito longas vêm em partes: se o resultado disser LEITURA PARCIAL, continue com start_line antes de responder. Leia antes de editar.",
            Parameters = Schema("""{"type":"object","properties":{"path":{"type":"string"},"start_line":{"type":"integer","minimum":1,"description":"Linha inicial (para continuar uma leitura parcial)"},"max_lines":{"type":"integer","minimum":1,"maximum":5000,"description":"Opcional; se a nota couber inteira, ela vem inteira mesmo assim"}},"required":["path"],"additionalProperties":false}"""),
            Label = i => "Lendo " + Str(i, "path") + (Int(i, "start_line") is > 1 ? " (a partir da linha " + Int(i, "start_line") + ")" : ""),
            Run = (i, host) =>
            {
                var d = ResolveDoc(host, Str(i, "path"))!;
                var r = ReadSlice(d, Int(i, "start_line"), Int(i, "max_lines"), ReadBudget(host));
                return Task.FromResult(new AiToolOutput(r.Text, Reads: [(d.Path, r.Partial)]));
            }
        },
        new()
        {
            Name = "read_notes", Access = "read",
            Description = "Lê várias notas de uma vez (até 10), cada uma inteira quando cabe. Use quando já sabe quais notas precisa, em vez de várias chamadas de read_note.",
            Parameters = Schema("""{"type":"object","properties":{"paths":{"type":"array","items":{"type":"string"},"minItems":1,"maxItems":10}},"required":["paths"],"additionalProperties":false}"""),
            Label = i => { int n = (i["paths"] as JsonArray)?.Count ?? 0; return "Lendo " + n + " nota" + (n == 1 ? "" : "s"); },
            Run = (i, host) =>
            {
                var paths = (i["paths"] as JsonArray ?? []).Take(10).Select(p => p is null ? "" : LegacyCustomize.JsToString(p)).ToList();
                int share = Math.Max(4000, ReadBudget(host) / Math.Max(1, paths.Count));
                var output = new List<string>();
                var reads = new List<(string, AiPartial?)>();
                foreach (var p in paths)
                {
                    try
                    {
                        var d = ResolveDoc(host, p)!;
                        var r = ReadSlice(d, 1, 0, share);
                        output.Add(r.Text);
                        reads.Add((d.Path, r.Partial));
                    }
                    catch (AiToolException e) { output.Add("# " + p + "\n(erro: " + e.Message + ")"); }
                }
                return Task.FromResult(new AiToolOutput(string.Join("\n\n━━━━━━━━\n\n", output), Reads: reads));
            }
        },
        new()
        {
            Name = "grep_notes", Access = "read",
            Description = "Procura um texto (ou expressão regular) dentro de todas as notas e devolve as linhas exatas com caminho e número da linha. Ignora maiúsculas e acentos no modo texto. Ótimo para achar onde algo é citado, nomes, datas e trechos para editar.",
            Parameters = Schema("""{"type":"object","properties":{"pattern":{"type":"string"},"regex":{"type":"boolean","description":"Tratar pattern como expressão regular (padrão false)"},"folder":{"type":"string","description":"Limitar a uma pasta"},"context":{"type":"integer","minimum":0,"maximum":3,"description":"Linhas de contexto antes/depois (padrão 0)"},"limit":{"type":"integer","minimum":1,"maximum":200,"description":"Máximo de linhas (padrão 60)"}},"required":["pattern"],"additionalProperties":false}"""),
            Label = i => { var p = Str(i, "pattern"); return "Procurando “" + (p.Length > 50 ? p[..50] : p) + "” nas notas"; },
            Run = (i, host) =>
            {
                var pat = Str(i, "pattern");
                var f = Norm(Str(i, "folder")).ToLowerInvariant();
                int lim = Int(i, "limit") is { } l && l != 0 ? l : 60, cx = Int(i, "context") ?? 0;
                if (LegacyCustomize.JsTrim(pat).Length == 0) throw new AiToolException("pattern vazio.");
                Func<string, bool> test;
                if (Has(i, "regex"))
                {
                    Regex re;
                    try { re = new Regex(pat, RegexOptions.IgnoreCase); }
                    catch (ArgumentException e) { throw new AiToolException("Expressão regular inválida: " + e.Message); }
                    test = re.IsMatch;
                }
                else { var q = Fold(pat); test = x => Fold(x).Contains(q, StringComparison.Ordinal); }
                var output = new List<string>();
                int hits = 0, files = 0;
                bool more = false;
                foreach (var d in host.Documents.List().OrderBy(d => d.Path, Comparer<string>.Create(JsText.LocaleCompare)))
                {
                    if (more || (f.Length > 0 && !d.Path.ToLowerInvariant().StartsWith(f + "/", StringComparison.Ordinal))) continue;
                    var lines = d.Content.Split('\n');
                    var shown = new HashSet<int>();
                    var block = new List<string>();
                    for (int n = 0; n < lines.Length; n++)
                    {
                        if (more || !test(lines[n])) continue;
                        if (hits >= lim) { more = true; continue; }
                        hits++;
                        for (int k = Math.Max(0, n - cx); k <= Math.Min(lines.Length - 1, n + cx); k++)
                            if (shown.Add(k)) block.Add("  " + (k + 1) + (k == n ? ": " : "- ") + Clip(lines[k], 300));
                    }
                    if (block.Count > 0) { files++; output.Add(d.Path + "\n" + string.Join("\n", block)); }
                }
                if (hits == 0) return Task.FromResult(new AiToolOutput("Nenhuma linha contém \"" + pat + "\"" + (f.Length > 0 ? " em " + Str(i, "folder") : "") + "."));
                return Task.FromResult(new AiToolOutput(hits + " linha(s) em " + files + " nota(s)" + (more ? " (limite atingido; refine a busca ou aumente limit)" : "") + ":\n\n" + string.Join("\n\n", output)));
            }
        },
        new()
        {
            Name = "get_links", Access = "read",
            Description = "Mostra as ligações de uma nota: links [[...]] que ela faz (e quais não existem), notas que apontam para ela (backlinks) e tags.",
            Parameters = Schema("""{"type":"object","properties":{"path":{"type":"string"}},"required":["path"],"additionalProperties":false}"""),
            Label = i => "Vendo ligações de " + Str(i, "path"),
            Run = (i, host) =>
            {
                var d = ResolveDoc(host, Str(i, "path"))!;
                var links = host.Knowledge.Links(d.Id);
                var output = links.Select(x => x.Path).ToList();
                var back = host.Knowledge.Backlinks(d.Id).Select(x => x.Path).ToList();
                var resolved = links.Select(x => Fold(x.Title)).ToHashSet(StringComparer.Ordinal);
                var missing = d.Links.Where(l => !resolved.Contains(Fold(l.Split('/')[^1]))).ToList();
                return Task.FromResult(new AiToolOutput("Links de " + d.Path + ":\n" + (output.Count > 0 ? string.Join("\n", output.Select(p => "- " + p)) : "- nenhum") +
                    (missing.Count > 0 ? "\nLinks para notas inexistentes: " + string.Join(", ", missing.Select(l => "[[" + l + "]]")) : "") +
                    "\n\nBacklinks:\n" + (back.Count > 0 ? string.Join("\n", back.Select(p => "- " + p)) : "- nenhum") +
                    "\n\nTags: " + (d.Tags.Count > 0 ? string.Join(" ", d.Tags.Select(t => "#" + t)) : "nenhuma")));
            }
        },
        new()
        {
            Name = "find_by_tag", Access = "read",
            Description = "Lista as notas que têm uma tag (#tag no texto ou tags no frontmatter).",
            Parameters = Schema("""{"type":"object","properties":{"tag":{"type":"string"}},"required":["tag"],"additionalProperties":false}"""),
            Label = i => "Notas com #" + Regex.Replace(Str(i, "tag"), "^#", ""),
            Run = (i, host) =>
            {
                var t = Regex.Replace(Str(i, "tag"), "^#", "");
                var hits = host.Knowledge.Tagged(t);
                return Task.FromResult(new AiToolOutput(hits.Count > 0 ? string.Join("\n", hits.Select(d => "- " + d.Path)) : "Nenhuma nota com #" + t + "."));
            }
        },
        new()
        {
            Name = "get_editor_context", Access = "read",
            Description = "Nota aberta no editor agora e o texto selecionado pelo usuário (se houver). Use quando o pedido falar de “esta nota”, “aqui”, “o trecho selecionado”.",
            Parameters = Schema("""{"type":"object","properties":{},"additionalProperties":false}"""),
            Label = _ => "Vendo o que está aberto",
            Run = (_, host) =>
            {
                var (path, selection) = host.EditorContext();
                if (string.IsNullOrEmpty(path)) return Task.FromResult(new AiToolOutput("Nenhuma nota aberta no editor."));
                return Task.FromResult(new AiToolOutput("Nota aberta: " + path + (!string.IsNullOrEmpty(selection) ? "\nSeleção do usuário:\n\"\"\"\n" + Clip(selection, 4000) + "\n\"\"\"" : "\nSem texto selecionado.")));
            }
        },
        new()
        {
            Name = "create_note", Access = "write",
            Description = "Cria uma nota nova. Falha se já existir (use edit_note ou write_note). Pastas no caminho são criadas. Use [[Título]] para ligar notas.",
            Parameters = Schema("""{"type":"object","properties":{"path":{"type":"string","description":"Ex.: \"Projetos/Plano.md\""},"content":{"type":"string"}},"required":["path","content"],"additionalProperties":false}"""),
            Label = i => "Criar " + Norm(Str(i, "path")),
            Preview = (i, _) => new AiPreview(ValidPath(Str(i, "path")), null, Str(i, "content")),
            Run = async (i, host) =>
            {
                var p = ValidPath(Str(i, "path"));
                if (host.Documents.Get(p) is not null) throw new AiToolException("Já existe " + p + ". Use edit_note ou write_note.");
                var d = await host.CreateNoteAsync(p, Str(i, "content"));
                return new AiToolOutput("Criada: " + d.Path + " (" + Words(d.Content) + " palavras).", [new AiChange(d.Id, null, Snap(d))]);
            }
        },
        new()
        {
            Name = "edit_note", Access = "write",
            Description = "Edita um trecho de uma nota: troca old_text (cópia exata, única na nota) por new_text. Preserva o resto. Prefira esta ferramenta para mudanças parciais.",
            Parameters = Schema("""{"type":"object","properties":{"path":{"type":"string"},"old_text":{"type":"string"},"new_text":{"type":"string"},"replace_all":{"type":"boolean"}},"required":["path","old_text","new_text"],"additionalProperties":false}"""),
            Label = i => "Editar " + Norm(Str(i, "path")),
            Preview = (i, host) =>
            {
                var d = ResolveDoc(host, Str(i, "path"))!;
                return new AiPreview(d.Path, d.Content, ApplyEdit(d.Content, Str(i, "old_text"), Str(i, "new_text"), Has(i, "replace_all")));
            },
            Run = async (i, host) =>
            {
                var d = ResolveDoc(host, Str(i, "path"))!;
                var next = ApplyEdit(d.Content, Str(i, "old_text"), Str(i, "new_text"), Has(i, "replace_all"));
                var u = await host.SaveNoteAsync(d, next);
                return new AiToolOutput("Editada: " + u.Path + ".", [new AiChange(d.Id, Snap(d), Snap(u))]);
            }
        },
        new()
        {
            Name = "write_note", Access = "write",
            Description = "Substitui todo o conteúdo de uma nota existente. Use só para reescritas completas; para mudanças parciais use edit_note.",
            Parameters = Schema("""{"type":"object","properties":{"path":{"type":"string"},"content":{"type":"string"}},"required":["path","content"],"additionalProperties":false}"""),
            Label = i => "Reescrever " + Norm(Str(i, "path")),
            Preview = (i, host) => { var d = ResolveDoc(host, Str(i, "path"))!; return new AiPreview(d.Path, d.Content, Str(i, "content")); },
            Run = async (i, host) =>
            {
                var d = ResolveDoc(host, Str(i, "path"))!;
                var u = await host.SaveNoteAsync(d, Str(i, "content"));
                return new AiToolOutput("Reescrita: " + u.Path + " (" + Words(u.Content) + " palavras).", [new AiChange(d.Id, Snap(d), Snap(u))]);
            }
        },
        new()
        {
            Name = "append_to_note", Access = "write",
            Description = "Acrescenta texto ao fim de uma nota, ou ao fim de uma seção (heading) específica.",
            Parameters = Schema("""{"type":"object","properties":{"path":{"type":"string"},"content":{"type":"string"},"heading":{"type":"string","description":"Título da seção, sem #"}},"required":["path","content"],"additionalProperties":false}"""),
            Label = i => "Acrescentar em " + Norm(Str(i, "path")),
            Preview = (i, host) =>
            {
                var d = ResolveDoc(host, Str(i, "path"))!;
                return new AiPreview(d.Path, d.Content, AppendText(d.Content, Str(i, "content"), Has(i, "heading") ? Str(i, "heading") : null));
            },
            Run = async (i, host) =>
            {
                var d = ResolveDoc(host, Str(i, "path"))!;
                var u = await host.SaveNoteAsync(d, AppendText(d.Content, Str(i, "content"), Has(i, "heading") ? Str(i, "heading") : null));
                return new AiToolOutput("Texto acrescentado em " + u.Path + ".", [new AiChange(d.Id, Snap(d), Snap(u))]);
            }
        },
        new()
        {
            Name = "rename_note", Access = "write",
            Description = "Renomeia ou move uma nota (novo caminho completo). Com update_links=true (padrão), atualiza os [[links]] das outras notas.",
            Parameters = Schema("""{"type":"object","properties":{"path":{"type":"string"},"new_path":{"type":"string"},"update_links":{"type":"boolean"}},"required":["path","new_path"],"additionalProperties":false}"""),
            Label = i => "Mover " + Norm(Str(i, "path")) + " → " + Norm(Str(i, "new_path")),
            Preview = (i, host) =>
            {
                var d = ResolveDoc(host, Str(i, "path"))!;
                bool links = !(i["update_links"] is JsonValue v && v.TryGetValue<bool>(out var b) && !b);
                return new AiPreview(d.Path + " → " + ValidPath(Str(i, "new_path")), null, null, "Renomear/mover" + (links ? " e atualizar links nas outras notas" : ""));
            },
            Run = async (i, host) =>
            {
                var d = ResolveDoc(host, Str(i, "path"))!;
                var np = ValidPath(Str(i, "new_path"));
                if (!string.Equals(np, d.Path, StringComparison.OrdinalIgnoreCase) && host.Documents.Get(np) is not null) throw new AiToolException("Já existe " + np + ".");
                var title = NoteRe.Replace(np.Split('/')[^1], "");
                var changes = new List<AiChange>();
                var u = await host.RenameNoteAsync(d, np);
                changes.Add(new AiChange(d.Id, Snap(d), Snap(u)));
                bool update = !(i["update_links"] is JsonValue v && v.TryGetValue<bool>(out var b) && !b);
                if (update && Fold(title) != Fold(d.Title))
                {
                    var re = new Regex(@"\[\[(" + Regex.Escape(d.Title) + @")(\|[^\]]*)?(#[^\]]*)?\]\]", RegexOptions.IgnoreCase);
                    foreach (var o in host.Documents.List().ToList())
                    {
                        if (o.Id == d.Id || !re.IsMatch(o.Content)) continue;
                        var c = re.Replace(o.Content, m => "[[" + title + m.Groups[3].Value + m.Groups[2].Value + "]]");
                        var uo = await host.SaveNoteAsync(o, c);
                        changes.Add(new AiChange(o.Id, Snap(o), Snap(uo)));
                    }
                }
                return new AiToolOutput("Movida para " + np + (changes.Count > 1 ? "; links atualizados em " + (changes.Count - 1) + " nota(s)" : "") + ".", changes);
            }
        },
        new()
        {
            Name = "delete_note", Access = "destructive",
            Description = "Move uma nota para a lixeira (pode ser restaurada). Só use quando o usuário pedir claramente.",
            Parameters = Schema("""{"type":"object","properties":{"path":{"type":"string"}},"required":["path"],"additionalProperties":false}"""),
            Label = i => "Excluir " + Norm(Str(i, "path")),
            Preview = (i, host) => { var d = ResolveDoc(host, Str(i, "path"))!; return new AiPreview(d.Path, d.Content, null, "Mover para a lixeira"); },
            Run = async (i, host) =>
            {
                var d = ResolveDoc(host, Str(i, "path"))!;
                await host.TrashNoteAsync(d);
                return new AiToolOutput("Movida para a lixeira: " + d.Path + ".", [new AiChange(d.Id, Snap(d), null, Trashed: true)]);
            }
        },
        new()
        {
            Name = "create_folder", Access = "write",
            Description = "Cria uma pasta no vault.",
            Parameters = Schema("""{"type":"object","properties":{"path":{"type":"string"}},"required":["path"],"additionalProperties":false}"""),
            Label = i => "Criar pasta " + Norm(Str(i, "path")),
            Preview = (i, _) => new AiPreview(Norm(Str(i, "path")) + "/", null, null, "Nova pasta"),
            Run = async (i, host) =>
            {
                var p = Norm(Str(i, "path"));
                if (p.Length == 0 || Regex.IsMatch(p, @"(^|/)\.")) throw new AiToolException("Pasta inválida.");
                await host.CreateFolderAsync(p);
                return new AiToolOutput("Pasta criada: " + p + ".");
            }
        },
        new()
        {
            Name = "open_note", Access = "ui",
            Description = "Abre uma nota no editor para o usuário ver o resultado.",
            Parameters = Schema("""{"type":"object","properties":{"path":{"type":"string"}},"required":["path"],"additionalProperties":false}"""),
            Label = i => "Abrir " + Str(i, "path"),
            Run = (i, host) =>
            {
                var d = ResolveDoc(host, Str(i, "path"))!;
                host.OpenNote(d.Path);
                return Task.FromResult(new AiToolOutput("Aberta: " + d.Path));
            }
        },
        new()
        {
            Name = "update_plan", Access = "plan",
            Description = "Mostra ao usuário o seu plano como uma lista de passos com status. Use em tarefas com 3 ou mais passos: crie o plano no começo e atualize a cada passo concluído (envie a lista inteira).",
            Parameters = Schema("""{"type":"object","properties":{"steps":{"type":"array","maxItems":20,"items":{"type":"object","properties":{"step":{"type":"string"},"status":{"type":"string","enum":["pending","in_progress","done"]}},"required":["step","status"]}}},"required":["steps"],"additionalProperties":false}"""),
            Label = i =>
            {
                var st = (i["steps"] as JsonArray ?? []).ToList();
                int d = st.Count(x => x is JsonObject o && Str(o, "status") == "done");
                return "Plano: " + d + "/" + st.Count + " passos";
            },
            Run = (i, host) =>
            {
                var st = (i["steps"] as JsonArray ?? []).OfType<JsonObject>().Where(x => LegacyCustomize.JsTrim(Str(x, "step")).Length > 0).Take(20)
                    .Select(x =>
                    {
                        var step = LegacyCustomize.JsTrim(Str(x, "step"));
                        var status = Str(x, "status");
                        return new AiPlanStep(step.Length > 200 ? step[..200] : step, status is "pending" or "in_progress" or "done" ? status : "pending");
                    }).ToList();
                if (st.Count == 0) throw new AiToolException("Envie ao menos um passo.");
                host.SetPlan(st);
                int d = st.Count(x => x.Status == "done");
                var next = st.FirstOrDefault(x => x.Status != "done");
                return Task.FromResult(new AiToolOutput("Plano atualizado: " + d + "/" + st.Count + " concluídos." + (next is not null ? " Próximo: " + next.Step : " Todos os passos concluídos; responda ao usuário.")));
            }
        },
        new()
        {
            Name = "remember", Access = "memory",
            Description = "Guarda um fato durável sobre o usuário ou o vault (preferências, convenções, objetivos) na memória do Assistente, usada em conversas futuras. Não guarde conteúdo de notas.",
            Parameters = Schema("""{"type":"object","properties":{"fact":{"type":"string"}},"required":["fact"],"additionalProperties":false}"""),
            Label = i => { var f = Str(i, "fact"); return "Memorizar: " + (f.Length > 60 ? f[..60] : f); },
            Run = (i, host) =>
            {
                var f = LegacyCustomize.JsTrim(Str(i, "fact"));
                if (f.Length == 0) throw new AiToolException("Fato vazio.");
                host.Remember(f);
                return Task.FromResult(new AiToolOutput("Memorizado."));
            }
        },
        AssistantAttachmentExtraction.Tool
    ];

    private static string Slice160(string s) => s.Length > 160 ? s[..160] : s;

    /// <summary>validate(tool, input): a light check against the schema; strings, integers, lists and booleans are coerced in place.</summary>
    public static List<string> Validate(AiTool tool, JsonNode? input0)
    {
        var sch = tool.Parameters;
        var props = sch["properties"] as JsonObject ?? new JsonObject();
        var errs = new List<string>();
        if (input0 is not JsonObject input) return ["os argumentos precisam ser um objeto JSON"];
        if (input.ContainsKey("__invalid") && input["__invalid"] is not null) return ["JSON dos argumentos inválido ou truncado"];
        foreach (var k in (sch["required"] as JsonArray ?? []).Select(x => (string)x!))
            if (input[k] is null || (IsEmpty(input[k]) && !(tool.Name == "edit_note" && k == "new_text"))) errs.Add("falta \"" + k + "\"");
        foreach (var k in input.Select(p => p.Key).ToList())
        {
            if (props[k] is not JsonObject p)
            {
                if (sch["additionalProperties"] is JsonValue ap && ap.TryGetValue<bool>(out var allowed) && !allowed) errs.Add("parâmetro desconhecido \"" + k + "\"");
                continue;
            }
            var v = input[k];
            var t = (string?)p["type"];
            if (t == "string" && !(v is JsonValue sv && sv.TryGetValue<string>(out _)))
            {
                if (v is JsonValue nv && nv.GetValueKind() == JsonValueKind.Number) input[k] = LegacyCustomize.JsToString(nv);
                else errs.Add("\"" + k + "\" deve ser texto");
            }
            if (t == "integer")
            {
                double n = LegacyCustomize.JsToNumber(v);
                if (!double.IsFinite(n) || Math.Floor(n) != n) errs.Add("\"" + k + "\" deve ser inteiro");
                else
                {
                    if (p["minimum"] is { } mi && n < mi.GetValue<double>()) n = mi.GetValue<double>();
                    if (p["maximum"] is { } ma && n > ma.GetValue<double>()) n = ma.GetValue<double>();
                    input[k] = n;   // a double, as JSON.parse would give it
                }
            }
            if (t == "array" && v is not JsonArray)
            {
                if (v is JsonValue s && s.TryGetValue<string>(out var str))
                {
                    try { if (JsonNode.Parse(str) is JsonArray pa) input[k] = v = pa; }
                    catch (JsonException) { input[k] = v = new JsonArray(JsonValue.Create(str)); }
                }
                if (input[k] is not JsonArray) errs.Add("\"" + k + "\" deve ser uma lista");
            }
            if (t == "array" && input[k] is JsonArray arr && p["items"] is JsonObject items && (string?)items["type"] == "string")
                input[k] = new JsonArray(arr.Select(x => (JsonNode)JsonValue.Create(x is null ? "null" : LegacyCustomize.JsToString(x))).ToArray());
            if (t == "boolean" && !(v is JsonValue bv && bv.TryGetValue<bool>(out _)))
            {
                if (v is JsonValue s2 && s2.TryGetValue<string>(out var b) && b is "true" or "false") input[k] = b == "true";
                else errs.Add("\"" + k + "\" deve ser true/false");
            }
        }
        return errs;
    }

    private static bool IsEmpty(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var value) && value.Length == 0;
}
