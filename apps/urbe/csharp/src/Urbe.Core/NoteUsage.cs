using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>One thing that depends on a note: a publication, a data block, a progress bar, an embed or a link.</summary>
public sealed record NoteDependent(string Kind, string Path, string Detail);

/// <summary>
/// CST-226: where a note is used — computed from the vault as it is now, never stored (no second source of truth).
/// Publications that reference it (by its stable id, with the section they take), ```dados blocks that read it,
/// progress bars counting its tasks, embeds <c>![[…]]</c> and links <c>[[…]]</c>. Shown before the note is changed
/// away or trashed, so its impact is understood; nothing dependent is changed by this.
/// </summary>
public static partial class NoteUsage
{
    public const string Publication = "publicação", Data = "dados", Progress = "progresso", Embed = "incorporação", Link = "ligação";

    public static IReadOnlyList<NoteDependent> Of(DocumentStore store, UrbeDocument note)
    {
        var result = new List<NoteDependent>();
        var name = Path.GetFileNameWithoutExtension(note.Path);
        var pathNoExt = note.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? note.Path[..^3] : note.Path;
        foreach (var doc in store.List())
        {
            if (doc.Id == note.Id) continue;
            if (doc.Path.EndsWith(".publication.json", StringComparison.OrdinalIgnoreCase))
            {
                PublicationProject? project = null;
                try { project = PublicationProjectCodec.Parse(doc.Content); }
                catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException or NotSupportedException) { }
                if (project is null) continue;
                foreach (var source in project.Sources.Where(s => s.DocumentId == note.Id))
                    result.Add(new NoteDependent(Publication, doc.Path, project.Title + (source.Fragment is { Length: > 0 } f ? " (trecho “" + f + "”)" : "")));
                continue;
            }
            if (!ArtifactModel.IsNote(doc.Path)) continue;
            var text = doc.Content;
            if (text.Contains("```", StringComparison.Ordinal))
            {
                foreach (var source in Urbe.Core.Data.DataBlock.Sources(text))
                {
                    var bare = source.Split('#')[0].Trim();
                    if (Same(bare, note.Path) || Same(bare + ".md", note.Path))
                        result.Add(new NoteDependent(Data, doc.Path, "lê a tabela " + (source.Contains('#') ? "sob “" + source[(source.IndexOf('#') + 1)..].Trim() + "”" : "da nota")));
                }
                if (text.Contains("note: " + note.Id, StringComparison.Ordinal))
                    result.Add(new NoteDependent(Progress, doc.Path, "conta as tarefas desta nota"));
            }
            int embeds = 0, links = 0;
            foreach (Match m in WikiRe().Matches(text))
            {
                var target = m.Groups[2].Value.Split('|')[0].Split('#')[0].Trim();
                if (!(Same(target, name) || Same(target, pathNoExt) || Same(target, note.Path))) continue;
                if (m.Groups[1].Value == "!") embeds++; else links++;
            }
            if (embeds > 0) result.Add(new NoteDependent(Embed, doc.Path, embeds == 1 ? "mostra esta nota" : "mostra esta nota " + embeds + " vezes"));
            if (links > 0) result.Add(new NoteDependent(Link, doc.Path, links == 1 ? "liga para esta nota" : links + " ligações"));
        }
        return result.OrderBy(r => Order(r.Kind)).ThenBy(r => r.Path, StringComparer.OrdinalIgnoreCase).ToList();

        static int Order(string kind) => kind switch { Publication => 0, Data => 1, Progress => 2, Embed => 3, _ => 4 };
        static bool Same(string a, string b) => string.Equals(a.Replace('\\', '/').Trim('/'), b.Replace('\\', '/').Trim('/'), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A sentence for a confirmation: what would lose this note (links are only counted).</summary>
    public static string? Summary(IReadOnlyList<NoteDependent> dependents)
    {
        var strong = dependents.Where(d => d.Kind != Link).ToList();
        if (strong.Count == 0) return null;
        var parts = strong.GroupBy(d => d.Kind).Select(g => g.Count() + " " + g.Key switch
        {
            Publication => g.Count() == 1 ? "publicação" : "publicações",
            Data => g.Count() == 1 ? "bloco de dados" : "blocos de dados",
            Progress => g.Count() == 1 ? "barra de progresso" : "barras de progresso",
            _ => g.Count() == 1 ? "incorporação" : "incorporações"
        });
        return "Usada em " + string.Join(", ", parts) + ": " + string.Join("; ", strong.Take(4).Select(d => Path.GetFileNameWithoutExtension(d.Path).Replace(".publication", "") + " — " + d.Detail)) +
               (strong.Count > 4 ? "…" : "") + ".";
    }

    [GeneratedRegex(@"(!?)\[\[([^\]\n]+)\]\]")]
    private static partial Regex WikiRe();
}
