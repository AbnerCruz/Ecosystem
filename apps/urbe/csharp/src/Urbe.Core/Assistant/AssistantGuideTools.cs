using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core.Assistant;

/// <summary>
/// CST-075: the agents read the Urbe's own documentation (the in-app Tutorial, updated with the app) instead of
/// guessing from other editors or plain Markdown — the editor's marks, boxes, links, embeds, formulas, requests in the
/// note, pages, templates and settings. Read-only; the articles come from the app (never from the vault).
/// </summary>
public static partial class AssistantGuideTools
{
    public const string Name = "urbe_guide";

    public sealed record Section(string Article, string Heading, string Text);

    /// <summary>The tool over <paramref name="articles"/> (path and Markdown of each Tutorial article, in reading order).</summary>
    public static AiTool Create(Func<IReadOnlyList<(string Path, string Text)>> articles) => new()
    {
        Name = Name, Access = "read",
        Description = "Consulta a documentação do próprio Numa (o Tutorial do app): como funcionam o editor e a sua sintaxe própria (comentários %%…%% e palavra{\"…\"}, " +
                      "tags de parágrafo #Tag:, caixas :::tipo, links [[…]] e incorporações ![[…]], fórmulas, pedidos &Nome&: na nota, selos de autoria), Área de trabalho, " +
                      "Explorer, modelos, flashcards, publicações, plugins, personalização, backups e atalhos. Use antes de explicar ao usuário como fazer algo no Numa ou de escolher como " +
                      "produzir algo no app; não presuma pelo que sabe de outros editores. Sem argumentos, lista os artigos e seções; com query, traz as seções mais relevantes; com article, o artigo inteiro.",
        Parameters = (JsonObject)JsonNode.Parse("""{"type":"object","properties":{"query":{"type":"string","description":"O que procurar, em poucas palavras (ex.: \"incorporar imagem\", \"caixa dentro de caixa\")"},"article":{"type":"string","description":"Título de um artigo para ler inteiro (ex.: \"Editor\")"}},"additionalProperties":false}""")!,
        Label = i => (string?)i["article"] is { Length: > 0 } a ? "Lendo o guia: " + a : (string?)i["query"] is { Length: > 0 } q ? "Consultando o guia: " + q : "Índice do guia",
        Run = (i, _) => Task.FromResult(new AiToolOutput(Answer(articles(), (string?)i["query"], (string?)i["article"])))
    };

    public static string Answer(IReadOnlyList<(string Path, string Text)> articles, string? query, string? article)
    {
        var sections = Sections(articles);
        if (!string.IsNullOrWhiteSpace(article))
        {
            var wanted = AssistantTools.Fold(article.Trim());
            var found = articles.FirstOrDefault(a => AssistantTools.Fold(Title(a.Path)) == wanted);
            if (found.Text is null) found = articles.FirstOrDefault(a => AssistantTools.Fold(Title(a.Path)).Contains(wanted, StringComparison.Ordinal));
            return found.Text is null
                ? "Não há artigo \"" + article.Trim() + "\". Artigos: " + string.Join(", ", articles.Select(a => Title(a.Path))) + "."
                : "# Guia do Numa — " + Title(found.Path) + "\n\n" + AssistantTools.Clip(found.Text.Trim(), 16000);
        }
        if (string.IsNullOrWhiteSpace(query))
            return "Guia do Numa — artigos e seções (use query para buscar ou article para ler um inteiro):\n" + string.Join("\n",
                articles.Select(a => "- " + Title(a.Path) + ": " + string.Join(" · ", sections.Where(s => s.Article == Title(a.Path) && s.Heading.Length > 0).Select(s => s.Heading))));
        var terms = Terms(query);
        // #1005 (beta.60: five searches for "flashcards" brought pages about other things): a word found in a good part
        // of the guide ("criar", "nota", "usar") says little; a section counts only when it has a specific word of the query
        var folded = sections.Select(s => AssistantTools.Fold(s.Article + " " + s.Heading + " " + s.Text)).ToList();
        var common = terms.Where(t => Generic.Contains(t) || folded.Count(f => f.Contains(Stem(t), StringComparison.Ordinal)) > Math.Max(2, sections.Count * 0.25)).ToHashSet(StringComparer.Ordinal);
        var specific = terms.Where(t => !common.Contains(t)).ToList();
        int Frequency(string t) => folded.Count(f => f.Contains(Stem(t), StringComparison.Ordinal));
        // the words that name the thing are nowhere in the guide, and the ones found are everyday words: not covered
        var missing = terms.Where(t => Frequency(t) == 0 && !Aliases.ContainsValue(t)).ToList();
        if (missing.Count > 0 && terms.Where(t => Frequency(t) > 0).All(common.Contains))
            return "Nada no guia do Numa para \"" + query.Trim() + "\" (" + string.Join(", ", missing) + " não aparece" + (missing.Count > 1 ? "m" : "") + "). Artigos: " +
                   string.Join(", ", articles.Select(a => Title(a.Path))) + ". Se o guia não cobre o assunto, diga isso ao usuário em vez de supor.";
        var ranked = sections
            .Select((s, k) => (Section: s, Score: specific.Count > 0 && !specific.Any(t => folded[k].Contains(Stem(t), StringComparison.Ordinal)) ? 0 : Score(s, terms, common)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(3)
            .ToList();
        if (ranked.Count == 0)
            return "Nada no guia do Numa para \"" + query.Trim() + "\". Artigos: " + string.Join(", ", articles.Select(a => Title(a.Path))) +
                   ". Tente outras palavras ou leia um artigo inteiro (article). Se o guia não cobre o assunto, diga isso ao usuário em vez de supor.";
        return "Guia do Numa — seções sobre \"" + query.Trim() + "\":\n\n" + AssistantTools.Clip(string.Join("\n\n━━━━━━━━\n\n",
            ranked.Select(x => "## " + x.Section.Article + (x.Section.Heading.Length > 0 ? " › " + x.Section.Heading : "") + "\n" + x.Section.Text.Trim())), 12000);
    }

    /// <summary>Each article split at its <c>## </c> headings (the lead before the first one is a section without heading).</summary>
    public static IReadOnlyList<Section> Sections(IReadOnlyList<(string Path, string Text)> articles)
    {
        var result = new List<Section>();
        foreach (var (path, text) in articles)
        {
            var title = Title(path);
            string heading = "";
            var lines = new List<string>();
            bool fence = false;
            void Flush() { if (lines.Any(l => l.Trim().Length > 0)) result.Add(new Section(title, heading, string.Join("\n", lines))); lines.Clear(); }
            foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
            {
                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal)) fence = !fence;
                if (!fence && line.StartsWith("## ", StringComparison.Ordinal)) { Flush(); heading = line[3..].Trim(); continue; }
                if (!fence && line.StartsWith("# ", StringComparison.Ordinal) && heading.Length == 0 && lines.Count == 0) continue;
                lines.Add(line);
            }
            Flush();
        }
        return result;
    }

    private static string Title(string path) => Path.GetFileNameWithoutExtension(path.Replace('\\', '/').Split('/')[^1]);

    private static readonly HashSet<string> Stop = new(StringComparer.Ordinal)
        { "como", "para", "uma", "umas", "uns", "com", "que", "nao", "por", "dos", "das", "nos", "nas", "pelo", "pela", "isso", "esse", "essa", "este", "esta", "fazer", "urbe", "the" };

    /// <summary>Words of a request that say what to do, not what about: they never pick a section alone.</summary>
    private static readonly HashSet<string> Generic = new(StringComparer.Ordinal)
        { "criar", "crie", "cria", "usar", "use", "usando", "utilizando", "abrir", "ver", "mudar", "colocar", "adicionar", "fazer", "faca", "faz", "nativo", "nativa", "nativos", "nativas", "ferramenta", "ferramentas", "recurso", "app", "numa", "tema", "sobre" };

    /// <summary>Other names people use for a thing the guide names one way.</summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["cartao"] = "flashcards", ["cartoes"] = "flashcards", ["card"] = "flashcards", ["cards"] = "flashcards", ["flashcard"] = "flashcards",
        ["memorizacao"] = "flashcards", ["memorizar"] = "flashcards", ["anki"] = "flashcards", ["decks"] = "flashcards", ["deck"] = "flashcards",
    };

    private static List<string> Terms(string query) =>
        WordRegex().Matches(AssistantTools.Fold(query)).Select(m => m.Value).Where(w => w.Length >= 3 && !Stop.Contains(w))
            .SelectMany(w => Aliases.TryGetValue(w, out var a) ? new[] { w, a } : [w]).Distinct().ToList();

    // a word's stem: "incorporar" finds "incorporação", "caixas" finds "caixa"
    private static string Stem(string t) => t.Length > 5 ? t[..(t.Length - 2)] : t.TrimEnd('s');

    private static double Score(Section s, List<string> terms, HashSet<string> common)
    {
        if (terms.Count == 0) return 0;
        var heading = AssistantTools.Fold(s.Article + " " + s.Heading);
        var body = AssistantTools.Fold(s.Text);
        double score = 0;
        foreach (var t in terms)
        {
            var stem = Stem(t);
            double weight = common.Contains(t) ? 0.25 : 1;
            if (heading.Contains(stem, StringComparison.Ordinal)) score += 3 * weight;
            int n = 0;
            for (int at = body.IndexOf(stem, StringComparison.Ordinal); at >= 0 && n < 5; at = body.IndexOf(stem, at + stem.Length, StringComparison.Ordinal)) n++;
            score += n * weight;
        }
        return score;
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRegex();
}
