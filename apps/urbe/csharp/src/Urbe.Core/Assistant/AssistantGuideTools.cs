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
        Description = "Consulta a documentação do próprio Urbe (o Tutorial do app): como funcionam o editor e a sua sintaxe própria (comentários %%…%% e palavra{\"…\"}, " +
                      "tags de parágrafo #Tag:, caixas :::tipo, links [[…]] e incorporações ![[…]], fórmulas, pedidos &Nome&: na nota, selos de autoria), Área de trabalho, " +
                      "Explorer, modelos, páginas, plugins, personalização, backups e atalhos. Use antes de explicar ao usuário como fazer algo no Urbe ou de escolher como " +
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
                : "# Guia do Urbe — " + Title(found.Path) + "\n\n" + AssistantTools.Clip(found.Text.Trim(), 16000);
        }
        if (string.IsNullOrWhiteSpace(query))
            return "Guia do Urbe — artigos e seções (use query para buscar ou article para ler um inteiro):\n" + string.Join("\n",
                articles.Select(a => "- " + Title(a.Path) + ": " + string.Join(" · ", sections.Where(s => s.Article == Title(a.Path) && s.Heading.Length > 0).Select(s => s.Heading))));
        var terms = Terms(query);
        var ranked = sections
            .Select(s => (Section: s, Score: Score(s, terms)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(3)
            .ToList();
        if (ranked.Count == 0)
            return "Nada no guia do Urbe para \"" + query.Trim() + "\". Artigos: " + string.Join(", ", articles.Select(a => Title(a.Path))) +
                   ". Tente outras palavras ou leia um artigo inteiro (article). Se o guia não cobre o assunto, diga isso ao usuário em vez de supor.";
        return "Guia do Urbe — seções sobre \"" + query.Trim() + "\":\n\n" + AssistantTools.Clip(string.Join("\n\n━━━━━━━━\n\n",
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

    private static List<string> Terms(string query) =>
        WordRegex().Matches(AssistantTools.Fold(query)).Select(m => m.Value).Where(w => w.Length >= 3 && !Stop.Contains(w)).Distinct().ToList();

    private static double Score(Section s, List<string> terms)
    {
        if (terms.Count == 0) return 0;
        var heading = AssistantTools.Fold(s.Article + " " + s.Heading);
        var body = AssistantTools.Fold(s.Text);
        double score = 0;
        foreach (var t in terms)
        {
            // a word's stem: "incorporar" finds "incorporação", "caixas" finds "caixa"
            var stem = t.Length > 5 ? t[..(t.Length - 2)] : t.TrimEnd('s');
            if (heading.Contains(stem, StringComparison.Ordinal)) score += 3;
            int n = 0;
            for (int at = body.IndexOf(stem, StringComparison.Ordinal); at >= 0 && n < 5; at = body.IndexOf(stem, at + stem.Length, StringComparison.Ordinal)) n++;
            score += n;
        }
        return score;
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRegex();
}
