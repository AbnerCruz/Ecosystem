using System.Text;
using System.Text.Json.Nodes;

namespace Urbe.Core;

/// <summary>
/// CST-157/159: an old page (<c>.page.json</c>) turned, on the person's command, into the parts of a publication. Text
/// written inside the page becomes real notes (the publication never holds authored text); blocks that show notes
/// become references to them; what has no equivalent is said in the report. The page file itself is never changed.
/// </summary>
public static class PageToPublication
{
    /// <summary>One piece in order: a note to create from the page's text, or an existing note (by its id).</summary>
    public sealed record Piece(string? Markdown, string? NoteId);

    public sealed record Result(IReadOnlyList<Piece> Pieces, IReadOnlyList<string> Report);

    public static Result Convert(PageDocument page, Func<string, string?> noteIdOfPath)
    {
        var pieces = new List<Piece>();
        var report = new List<string>();
        var text = new StringBuilder();
        void Flush() { if (text.Length > 0) { pieces.Add(new(text.ToString().Trim() + "\n", null)); text.Clear(); } }
        void Note(string path)
        {
            var clean = path.Trim().TrimStart('/');
            if (clean.Length == 0) return;
            if ((noteIdOfPath(clean) ?? noteIdOfPath(clean + ".md")) is { } id) { Flush(); pieces.Add(new(null, id)); }
            else report.Add("Nota não encontrada: " + clean);
        }
        void Add(string? markdown) { if (!string.IsNullOrWhiteSpace(markdown)) text.Append(markdown.Trim()).Append("\n\n"); }
        static string S(JsonObject p, string key) => p[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
        var counts = new Dictionary<string, int>();
        foreach (var section in page.Sections)
        {
            var p = section.Props;
            var kind = section.Type;
            switch (kind)
            {
                case "text": Add(S(p, "markdown")); break;
                case "hero" or "chapter" or "toc" when S(p, "title").Length > 0:
                    Add("# " + S(p, "title") + (S(p, "subtitle").Length > 0 ? "\n\n" + S(p, "subtitle") : ""));
                    if (kind == "chapter" && S(p, "source").Length > 0) Note(S(p, "source"));
                    break;
                case "cta": Add("## " + S(p, "title") + "\n\n" + S(p, "text")); break;
                case "quote": Add("> " + S(p, "text").Replace("\n", "\n> ") + (S(p, "author").Length > 0 ? "\n>\n> — " + S(p, "author") : "")); break;
                case "code": Add("```" + S(p, "language") + "\n" + S(p, "code") + "\n```"); break;
                case "image" when S(p, "src").Length > 0:
                    Add((S(p, "src").Contains("://") ? "![" + S(p, "alt") + "](" + S(p, "src") + ")" : "![[" + S(p, "src") + "]]") + (S(p, "caption").Length > 0 ? "\n\n" + S(p, "caption") : ""));
                    break;
                case "video" when S(p, "url").Length > 0: Add(S(p, "url") + (S(p, "caption").Length > 0 ? "\n\n" + S(p, "caption") : "")); break;
                case "divider": Add("---"); break;
                case "note": Note(S(p, "path")); break;
                case "notes" when S(p, "source") == "list":
                    foreach (var path in S(p, "paths").Split('\n')) Note(path);
                    break;
                case "notes":
                    report.Add("Lista automática de notas (" + (S(p, "source") is { Length: > 0 } source ? source : "recentes") +
                        (S(p, "folder").Length > 0 ? ": " + S(p, "folder") : S(p, "tag").Length > 0 ? ": #" + S(p, "tag").TrimStart('#') : "") +
                        ") — adicione as notas pelo estúdio, ou crie a publicação da pasta ou da tag.");
                    break;
                default:
                    counts[kind.Length > 0 ? kind : "sem tipo"] = counts.GetValueOrDefault(kind.Length > 0 ? kind : "sem tipo") + 1;
                    break;
            }
        }
        Flush();
        foreach (var (kind, n) in counts) report.Add("Bloco “" + kind + "” sem equivalente (" + n + "): continua só na página antiga.");
        report.Add("O visual da página (tema, cores, colunas) não é levado: escolha o tema da publicação no estúdio.");
        return new(pieces, report);
    }
}
