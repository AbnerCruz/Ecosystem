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

    public static IReadOnlyList<NoteDependent> Of(DocumentStore store, UrbeDocument note) => OfMany(store, [note])[note.Id];

    /// <summary>
    /// What depends on each of <paramref name="notes"/> (by id), in ONE pass over the vault: many notes chosen to go to
    /// the trash no longer read every note of the vault once each before the question shows (observation of
    /// 15/11/2026: "o botão de excluir notas ... delay absurdo").
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<NoteDependent>> OfMany(DocumentStore store, IReadOnlyList<UrbeDocument> notes)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(notes);
        var found = new Dictionary<string, List<NoteDependent>>(StringComparer.Ordinal);
        // the names a link may use for each note: its name, its path without .md, its path
        var byName = new Dictionary<string, List<UrbeDocument>>(StringComparer.OrdinalIgnoreCase);
        void Name(string key, UrbeDocument n)
        {
            key = Norm(key);
            if (!byName.TryGetValue(key, out var list)) byName[key] = list = [];
            if (!list.Contains(n)) list.Add(n);
        }
        foreach (var n in notes)
        {
            if (found.ContainsKey(n.Id)) continue;
            found[n.Id] = [];
            Name(Path.GetFileNameWithoutExtension(n.Path), n);
            Name(n.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? n.Path[..^3] : n.Path, n);
            Name(n.Path, n);
        }
        if (found.Count == 0) return new Dictionary<string, IReadOnlyList<NoteDependent>>();
        foreach (var doc in store.List())
        {
            if (doc.Path.EndsWith(".publication.json", StringComparison.OrdinalIgnoreCase))
            {
                PublicationProject? project = null;
                try { project = PublicationProjectCodec.Parse(doc.Content); }
                catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException or NotSupportedException) { }
                if (project is null) continue;
                foreach (var source in project.Sources)
                    if (source.DocumentId is { } id && id != doc.Id && found.TryGetValue(id, out var list))
                        list.Add(new NoteDependent(Publication, doc.Path, project.Title + (source.Fragment is { Length: > 0 } f ? " (trecho “" + f + "”)" : "")));
                continue;
            }
            if (!ArtifactModel.IsNote(doc.Path)) continue;
            var text = doc.Content;
            if (text.Contains("```", StringComparison.Ordinal))
            {
                foreach (var source in Urbe.Core.Data.DataBlock.Sources(text))
                {
                    var bare = source.Split('#')[0].Trim();
                    foreach (var n in Targets(bare).Concat(Targets(bare + ".md")).Distinct())
                        if (n.Id != doc.Id && Same(n.Path, bare, bare + ".md"))
                            found[n.Id].Add(new NoteDependent(Data, doc.Path, "lê a tabela " + (source.Contains('#') ? "sob “" + source[(source.IndexOf('#') + 1)..].Trim() + "”" : "da nota")));
                }
                foreach (var id in found.Keys)
                    if (id != doc.Id && text.Contains("note: " + id, StringComparison.Ordinal))
                        found[id].Add(new NoteDependent(Progress, doc.Path, "conta as tarefas desta nota"));
            }
            Dictionary<string, (int Embeds, int Links)>? counts = null;
            foreach (Match m in WikiRe().Matches(text))
            {
                var target = m.Groups[2].Value.Split('|')[0].Split('#')[0].Trim();
                foreach (var n in Targets(target))
                {
                    if (n.Id == doc.Id) continue;
                    counts ??= new(StringComparer.Ordinal);
                    var (e, l) = counts.GetValueOrDefault(n.Id);
                    counts[n.Id] = m.Groups[1].Value == "!" ? (e + 1, l) : (e, l + 1);
                }
            }
            foreach (var (id, (embeds, links)) in counts ?? [])
            {
                if (embeds > 0) found[id].Add(new NoteDependent(Embed, doc.Path, embeds == 1 ? "mostra esta nota" : "mostra esta nota " + embeds + " vezes"));
                if (links > 0) found[id].Add(new NoteDependent(Link, doc.Path, links == 1 ? "liga para esta nota" : links + " ligações"));
            }
        }
        return found.ToDictionary(p => p.Key, p => (IReadOnlyList<NoteDependent>)p.Value
            .OrderBy(r => Order(r.Kind)).ThenBy(r => r.Path, StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.Ordinal);

        IEnumerable<UrbeDocument> Targets(string target) => byName.TryGetValue(Norm(target), out var list) ? list : [];
        static string Norm(string p) => p.Replace('\\', '/').Trim('/');
        static bool Same(string path, params string[] names) => names.Any(n => string.Equals(Norm(n), Norm(path), StringComparison.OrdinalIgnoreCase));
        static int Order(string kind) => kind switch { Publication => 0, Data => 1, Progress => 2, Embed => 3, _ => 4 };
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
