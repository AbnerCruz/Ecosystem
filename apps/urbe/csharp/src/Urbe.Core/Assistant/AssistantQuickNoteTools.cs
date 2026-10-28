using System.Text.Json.Nodes;

namespace Urbe.Core.Assistant;

/// <summary>
/// CST-053: an agent's quick note goes where the person's and the widget's go — the month's note in "Notas rápidas", in
/// the day's section, as a checkbox with the time (QuickNotes). The agent sends only the text.
/// </summary>
public static class AssistantQuickNoteTools
{
    public static AiTool Add { get; } = new()
    {
        Name = "add_quick_note", Access = "write",
        Description = "Registra uma nota rápida em \"" + QuickNotes.Folder + "/aaaa-mm.md\", na seção do dia, como item de checklist com o horário atual. O Urbe cria a nota do mês, a seção e o checkbox; envie só o texto. Use quando o usuário pedir para anotar, lembrar ou registrar algo rapidamente.",
        Parameters = (JsonObject)JsonNode.Parse("""{"type":"object","properties":{"text":{"type":"string","description":"O que anotar, numa frase"}},"required":["text"],"additionalProperties":false}""")!,
        Label = i => { var t = ((string?)i["text"] ?? "").Trim(); return "Nota rápida: " + (t.Length > 40 ? t[..40] + "…" : t); },
        Preview = (i, host) =>
        {
            var now = DateTime.Now;
            var path = QuickNotes.PathFor(now);
            var before = host.Documents.Get(path)?.Content;
            var made = QuickNotes.Append(before, (string?)i["text"], now) ?? throw new AiToolException("Texto vazio.");
            return new AiPreview(made.Path, before, made.Content);
        },
        Run = async (i, host) =>
        {
            var now = DateTime.Now;
            var path = QuickNotes.PathFor(now);
            var doc = host.Documents.Get(path);
            var made = QuickNotes.Append(doc?.Content, (string?)i["text"], now) ?? throw new AiToolException("Texto vazio.");
            var saved = doc is null ? await host.CreateNoteAsync(made.Path, made.Content) : await host.SaveNoteAsync(doc, made.Content);
            return new AiToolOutput("Nota rápida registrada em " + saved.Path + ".",
                [new AiChange(saved.Id, doc is null ? null : new AiNoteSnap(doc.Path, doc.Content), new AiNoteSnap(saved.Path, saved.Content))]);
        }
    };
}
