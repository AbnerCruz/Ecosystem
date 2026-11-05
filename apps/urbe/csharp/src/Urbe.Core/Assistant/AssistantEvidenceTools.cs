using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core.Assistant;

/// <summary>
/// CST-223: the agents justify what they say about the vault with citable passages — the note, the section, the lines,
/// the exact text and the note's revision — ranked lexically (BM25 over paragraphs, accents and case folded, light
/// Portuguese stems; no model, no network). Nothing is invented: without a passage that holds the terms, the answer
/// says so. <c>cite_check</c> (CST-226) re-verifies a passage before it is reused: intact, present, moved, changed or gone.
/// Read-only; only the person's notes are read (never Personalização or hidden folders), without comments %%…%%,
/// notes {"…"}, authorship marks or requests &amp;Nome&amp;:.
/// </summary>
public static partial class AssistantEvidenceTools
{
    public const string FindName = "find_evidence";
    public const string CheckName = "cite_check";

    /// <summary>The longest passage quoted as is; a longer paragraph is split by lines, a longer line is cut around the match.</summary>
    public const int PassageChars = 800;

    private const double K1 = 1.2, B = 0.75;
    private const int PerNote = 3;

    /// <summary>A citable passage: lines From–To of the note at <see cref="Revision"/>, under <see cref="Section"/>.</summary>
    public sealed record Passage(string Path, IReadOnlyList<string> Section, int From, int To, string Text, string Revision, bool Cut, double Score, IReadOnlyList<string> Matched);

    public static AiTool Find { get; } = new()
    {
        Name = FindName, Access = "read",
        Description = "Encontra nas notas os trechos que sustentam (ou não) uma pergunta ou afirmação, para responder com citação. Devolve, do mais ao menos relevante: " +
                      "caminho da nota, seção (títulos), linhas, o texto exato do trecho, a revisão da nota, os termos que casaram e notas ligadas (links/backlinks) que também tratam do assunto. " +
                      "Ignora acentos e maiúsculas; não lê Personalização nem comentários %%…%%. Se nada sustentar a pergunta, diz isso: então diga ao usuário que as notas não tratam do assunto, sem supor. " +
                      "Cite o caminho e as linhas ao usar um trecho; antes de reutilizar um trecho citado antes, confirme com cite_check.",
        Parameters = Schema("""{"type":"object","properties":{"query":{"type":"string","description":"A pergunta ou afirmação a justificar, em palavras do assunto (ex.: \"prazo de entrega do projeto Aurora\")"},"folder":{"type":"string","description":"Limitar a uma pasta"},"limit":{"type":"integer","minimum":1,"maximum":15,"description":"Máximo de trechos (padrão 6)"}},"required":["query"],"additionalProperties":false}"""),
        Label = i => "Buscando evidências de “" + Short(Str(i, "query")) + "”",
        Run = (i, host) => Task.FromResult(new AiToolOutput(FindText(host, Str(i, "query"), Str(i, "folder"), Int(i, "limit"))))
    };

    public static AiTool Check { get; } = new()
    {
        Name = CheckName, Access = "read",
        Description = "Confere se um trecho citado antes (por find_evidence ou read_note) ainda está na nota: INTACTO (mesma revisão), PRESENTE (a nota mudou, o trecho não), " +
                      "MOVIDO (outras linhas ou outra nota), ALTERADO (mostra o texto parecido de agora) ou SUMIU. Use antes de reutilizar uma evidência numa resposta ou numa escrita.",
        Parameters = Schema("""{"type":"object","properties":{"path":{"type":"string"},"passage":{"type":"string","description":"O texto exato do trecho, como foi citado"},"revision":{"type":"string","description":"A revisão citada (ex.: \"3f2a9c0b1d2e\")"},"start_line":{"type":"integer","minimum":1,"description":"A linha em que o trecho começava"}},"required":["path","passage"],"additionalProperties":false}"""),
        Label = i => "Conferindo citação em " + AssistantTools.Norm(Str(i, "path")),
        Run = (i, host) => Task.FromResult(new AiToolOutput(CheckText(host, Str(i, "path"), Str(i, "passage"), Str(i, "revision"), Int(i, "start_line"))))
    };

    public static IReadOnlyList<AiTool> All { get; } = [Find, Check];

    // ---------------------------------------------------------------- the view the evidence is read from

    /// <summary>Only the person's notes: no Personalização, plugins, themes or hidden folders.</summary>
    public static bool Eligible(string path) => ArtifactModel.IsNote(path);

    /// <summary>The revision identity of a note's readable text: the first 12 hex digits of its SHA-256.</summary>
    public static string RevisionOf(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant()[..12];

    /// <summary>
    /// The text the evidence is read from: comments, notes, authorship marks and requests to agents blanked out while
    /// every line keeps its number (as read_note numbers them); code keeps its text.
    /// </summary>
    public static string Masked(string text)
    {
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (!text.Contains("%%", StringComparison.Ordinal) && !text.Contains("{\"", StringComparison.Ordinal) && !text.Contains("{ia:", StringComparison.Ordinal) && !text.Contains("&:", StringComparison.Ordinal)) return text;
        var sb = new StringBuilder(text.Length);
        int at = 0;
        foreach (Match code in CodeRegex().Matches(text))
        {
            sb.Append(MaskPart(text[at..code.Index]));
            sb.Append(code.Value);
            at = code.Index + code.Length;
        }
        sb.Append(MaskPart(text[at..]));
        return sb.ToString();

        static string MaskPart(string part) =>
            MarksRegex().Replace(AgentLineRegex().Replace(part, ""), m => new string('\n', m.Value.Count(c => c == '\n')));
    }

    /// <summary>Only Markdown notes carry authorship seals (as read_note reads them).</summary>
    private static string ReadableText(IAssistantHost host, UrbeDocument d) =>
        d.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || d.Path.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase) ? host.Readable(d.Content) : d.Content;

    // ---------------------------------------------------------------- terms

    private static readonly HashSet<string> Stop = new(StringComparer.Ordinal)
    {
        "a", "o", "e", "as", "os", "um", "uma", "uns", "umas", "de", "do", "da", "dos", "das", "em", "no", "na", "nos", "nas", "num", "numa",
        "por", "pelo", "pela", "pelos", "pelas", "para", "pra", "com", "sem", "sob", "sobre", "que", "qual", "quais", "quem", "como", "onde", "quando",
        "porque", "pois", "se", "ao", "aos", "ja", "la", "ou", "mas", "nem", "mais", "menos", "muito", "muita", "muitos", "muitas", "tambem",
        "e", "era", "foi", "ser", "sao", "estar", "esta", "estao", "estava", "ter", "tem", "teve", "ha", "havia", "seja", "sera",
        "isso", "isto", "esse", "essa", "esses", "essas", "este", "estes", "estas", "aquele", "aquela", "aquilo", "ele", "ela", "eles", "elas",
        "eu", "voce", "voces", "nos", "me", "te", "lhe", "lhes", "meu", "minha", "meus", "minhas", "seu", "sua", "seus", "suas", "nosso", "nossa",
        "nao", "sim", "entao", "ate", "apos", "entre", "cada", "todo", "toda", "todos", "todas", "outro", "outra", "mesmo", "mesma",
        "the", "of", "and", "to", "in", "is", "are", "was", "for", "on", "with", "what", "which", "who", "how", "an"
    };

    private static readonly string[] Suffixes =
        ["amentos", "imentos", "amento", "imento", "mente", "acoes", "icoes", "acao", "icao", "adoras", "adores", "adora", "ador", "idades", "idade",
         "ando", "endo", "indo", "ados", "adas", "idos", "idas", "ado", "ada", "ido", "ida", "ar", "er", "ir", "as", "os", "es", "a", "o", "e"];

    /// <summary>A folded word reduced to a light Portuguese stem (plural, gender, common verb and noun endings).</summary>
    public static string Stem(string w)
    {
        if (w.Length <= 3 || w.All(char.IsDigit)) return w;
        // plurals first: ões/ães → ão, ais → al, eis → el, ns → m
        if (w.EndsWith("oes", StringComparison.Ordinal) || w.EndsWith("aes", StringComparison.Ordinal)) w = w[..^3] + "ao";
        else if (w.EndsWith("ais", StringComparison.Ordinal)) w = w[..^3] + "al";
        else if (w.EndsWith("eis", StringComparison.Ordinal)) w = w[..^3] + "el";
        else if (w.EndsWith("ns", StringComparison.Ordinal)) w = w[..^2] + "m";
        else if (w.EndsWith('s') && !w.EndsWith("ss", StringComparison.Ordinal)) w = w[..^1];
        foreach (var s in Suffixes)
            if (w.Length - s.Length >= 4 && w.EndsWith(s, StringComparison.Ordinal)) return w[..^s.Length];
        return w;
    }

    /// <summary>The terms of a text: folded words, without stop words, each with its stem.</summary>
    public static IEnumerable<(string Word, string Stem)> Terms(string text)
    {
        foreach (Match m in WordRegex().Matches(AssistantTools.Fold(text)))
        {
            var w = m.Value;
            if (w.Length < 2 || Stop.Contains(w)) continue;
            yield return (w, Stem(w));
        }
    }

    // ---------------------------------------------------------------- passages

    private sealed record Unit(int From, int To, string Text, IReadOnlyList<string> Section, Dictionary<string, int> Tf, int Length, HashSet<string> HeadingStems);

    /// <summary>The note's paragraphs (split by blank lines, headings and long runs), each under its heading path.</summary>
    private static List<Unit> Units(string masked, string title)
    {
        var lines = masked.Split('\n');
        var units = new List<Unit>();
        var headings = new List<(int Level, string Text)>();
        var titleStems = Terms(title).Select(t => t.Stem).ToHashSet(StringComparer.Ordinal);
        int start = 0;
        if (lines.Length > 0 && lines[0].TrimEnd() == "---")
            for (int j = 1; j < lines.Length; j++)
                if (lines[j].TrimEnd() is "---" or "...") { start = j + 1; break; }
        var run = new List<int>();
        int chars = 0;
        bool fence = false;
        void Flush()
        {
            if (run.Count > 0)
            {
                var text = string.Join("\n", run.Select(n => lines[n]));
                if (text.Trim().Length > 0)
                {
                    var tf = new Dictionary<string, int>(StringComparer.Ordinal);
                    int length = 0;
                    foreach (var (_, stem) in Terms(text)) { tf[stem] = tf.GetValueOrDefault(stem) + 1; length++; }
                    var section = headings.Select(h => h.Text).ToList();
                    var heading = new HashSet<string>(titleStems, StringComparer.Ordinal);
                    foreach (var h in section) foreach (var (_, stem) in Terms(h)) heading.Add(stem);
                    units.Add(new Unit(run[0] + 1, run[^1] + 1, text, section, tf, length, heading));
                }
            }
            run.Clear();
            chars = 0;
        }
        for (int n = start; n < lines.Length; n++)
        {
            var line = lines[n];
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal)) fence = !fence;
            else if (!fence)
            {
                var h = HeadingRegex().Match(line);
                if (h.Success)
                {
                    Flush();
                    int level = h.Groups[1].Length;
                    headings.RemoveAll(x => x.Level >= level);
                    headings.Add((level, Regex.Replace(h.Groups[2].Value, @"\s+#+\s*$", "").Trim()));
                    continue;
                }
                if (line.Trim().Length == 0) { Flush(); continue; }
            }
            if (run.Count > 0 && chars + line.Length + 1 > PassageChars) Flush();
            run.Add(n);
            chars += line.Length + 1;
        }
        Flush();
        return units;
    }

    /// <summary>
    /// The passages of the eligible notes (in <paramref name="folder"/>) that best hold <paramref name="query"/>, best
    /// first: Okapi BM25 over the paragraphs' stems, weighted by how many of the query's terms the passage has; a term
    /// only in the section's heading or the note's title counts a little, and a passage without any term in its own text is not evidence. Ties go by path and line (deterministic).
    /// </summary>
    public static (IReadOnlyList<Passage> Passages, IReadOnlyList<string> Terms, IReadOnlyList<string> Absent) Rank(IAssistantHost host, string query, string? folder = null, int limit = 6)
    {
        var wanted = new List<(string Word, string Stem)>();
        foreach (var t in Terms(query)) if (!wanted.Any(w => w.Stem == t.Stem)) wanted.Add(t);
        if (wanted.Count == 0) return ([], [], []);
        var f = AssistantTools.Norm(folder).ToLowerInvariant();
        var notes = new List<(UrbeDocument Doc, string Revision, List<Unit> Units)>();
        foreach (var d in host.Documents.List())
        {
            if (!Eligible(d.Path) || (f.Length > 0 && !d.Path.ToLowerInvariant().StartsWith(f + "/", StringComparison.Ordinal))) continue;
            var readable = ReadableText(host, d);
            notes.Add((d, RevisionOf(readable), Units(Masked(readable), d.Title)));
        }
        int total = notes.Sum(n => n.Units.Count);
        if (total == 0) return ([], wanted.Select(w => w.Word).ToList(), wanted.Select(w => w.Word).ToList());
        double avg = Math.Max(1, notes.Sum(n => n.Units.Sum(u => u.Length)) / (double)total);
        var df = wanted.ToDictionary(w => w.Stem, w => notes.Sum(n => n.Units.Count(u => u.Tf.ContainsKey(w.Stem))), StringComparer.Ordinal);
        var idf = df.ToDictionary(p => p.Key, p => Math.Log(1 + (total - p.Value + 0.5) / (p.Value + 0.5)), StringComparer.Ordinal);
        var found = new List<Passage>();
        foreach (var (doc, revision, units) in notes)
            foreach (var u in units)
            {
                double score = 0;
                int inText = 0;
                var matched = new List<string>();
                foreach (var (word, stem) in wanted)
                {
                    if (u.Tf.TryGetValue(stem, out var tf))
                    {
                        score += idf[stem] * tf * (K1 + 1) / (tf + K1 * (1 - B + B * u.Length / avg));
                        matched.Add(word);
                        inText++;
                    }
                    else if (u.HeadingStems.Contains(stem))
                    {
                        score += 0.3 * idf[stem];
                        matched.Add(word);
                    }
                }
                // the passage itself must hold a term: a heading alone is not evidence
                if (inText == 0) continue;
                score *= 0.5 + 0.5 * matched.Count / wanted.Count;
                var (text, cut) = Excerpt(u.Text, wanted.Select(w => w.Stem).ToList());
                found.Add(new Passage(doc.Path, u.Section, u.From, u.To, text, revision, cut, score, matched));
            }
        var ranked = found
            .OrderByDescending(p => p.Score)
            .ThenBy(p => p.Path, StringComparer.Ordinal)
            .ThenBy(p => p.From)
            .GroupBy(p => p.Path, StringComparer.Ordinal)
            .SelectMany(g => g.Take(PerNote))
            .OrderByDescending(p => p.Score)
            .ThenBy(p => p.Path, StringComparer.Ordinal)
            .ThenBy(p => p.From)
            .Take(Math.Clamp(limit, 1, 15))
            .ToList();
        return (ranked, wanted.Select(w => w.Word).ToList(), wanted.Where(w => df[w.Stem] == 0).Select(w => w.Word).ToList());
    }

    /// <summary>The passage as is, or — a single line longer than <see cref="PassageChars"/> — the stretch around its first match.</summary>
    private static (string Text, bool Cut) Excerpt(string text, List<string> stems)
    {
        if (text.Length <= PassageChars) return (text, false);
        int at = 0;
        foreach (Match m in WordRegex().Matches(text))
            if (stems.Contains(Stem(AssistantTools.Fold(m.Value)))) { at = m.Index; break; }
        int from = Math.Clamp(at - PassageChars / 4, 0, text.Length - PassageChars);
        return (text.Substring(from, PassageChars), true);
    }

    // ---------------------------------------------------------------- find_evidence

    public static string FindText(IAssistantHost host, string query, string? folder, int? limit)
    {
        var q = query.Trim();
        if (q.Length == 0) throw new AiToolException("Pergunta vazia.");
        var (passages, terms, absent) = Rank(host, q, folder, limit is > 0 ? limit.Value : 6);
        if (terms.Count == 0) throw new AiToolException("A pergunta não tem termos para procurar (só palavras comuns). Use as palavras do assunto.");
        var where = string.IsNullOrWhiteSpace(folder) ? "no vault" : "em " + AssistantTools.Norm(folder);
        if (passages.Count == 0)
            return "Nenhum trecho " + where + " contém os termos de \"" + q + "\" (procurados: " + string.Join(", ", terms) + "). " +
                   "Não há evidência nas notas para isso: diga ao usuário que as notas não tratam do assunto, sem supor. Tente sinônimos, outra pasta ou search_notes.";
        var sb = new StringBuilder();
        int notes = passages.Select(p => p.Path).Distinct(StringComparer.Ordinal).Count();
        sb.Append("Evidências para \"").Append(q).Append("\" (termos: ").Append(string.Join(", ", terms)).Append(") — ")
          .Append(passages.Count).Append(passages.Count == 1 ? " trecho" : " trechos").Append(" em ").Append(notes).Append(notes == 1 ? " nota" : " notas").Append('.');
        if (absent.Count > 0) sb.Append("\nSem nenhuma ocorrência ").Append(where).Append(": ").Append(string.Join(", ", absent)).Append('.');
        if (passages[0].Matched.Count < terms.Count)
            sb.Append("\n⚠ Evidência parcial: nenhum trecho tem todos os termos. Diga o que as notas sustentam e o que não sustentam.");
        var cited = passages.Select(p => p.Path).ToHashSet(StringComparer.Ordinal);
        var stems = terms.Select(Stem).ToList();
        var shown = new HashSet<string>(StringComparer.Ordinal);
        for (int k = 0; k < passages.Count; k++)
        {
            var p = passages[k];
            sb.Append("\n\n[").Append(k + 1).Append("] ").Append(p.Path);
            if (p.Section.Count > 0) sb.Append(" › ").Append(string.Join(" › ", p.Section));
            sb.Append(" · ").Append(p.From == p.To ? "linha " + p.From : "linhas " + p.From + "–" + p.To)
              .Append(" · revisão ").Append(p.Revision)
              .Append("\ncasou: ").Append(string.Join(", ", p.Matched)).Append(" (").Append(p.Matched.Count).Append('/').Append(terms.Count).Append(" termos)")
              .Append(p.Cut ? " · recorte de um parágrafo maior" : "")
              .Append("\n\"\"\"\n").Append(p.Text).Append("\n\"\"\"");
            // the related notes once, under the note's first passage
            if (shown.Add(p.Path) && Related(host, p.Path, stems, cited) is { Length: > 0 } links) sb.Append("\nligadas: ").Append(links);
        }
        sb.Append("\n\nAo usar um trecho, cite o caminho e as linhas. Para reutilizá-lo depois, confirme com cite_check (path, passage, revision).");
        return AssistantTools.Clip(sb.ToString());
    }

    /// <summary>The notes linked to or from <paramref name="path"/> that also have the query's terms.</summary>
    private static string Related(IAssistantHost host, string path, List<string> stems, HashSet<string> cited)
    {
        if (host.Documents.Get(path) is not { } d) return "";
        var output = new List<string>();
        void Add(UrbeDocument other, string arrow)
        {
            if (output.Count >= 4 || other.Path == path || !Eligible(other.Path)) return;
            var have = Terms(Masked(ReadableText(host, other)) + " " + other.Title).Select(t => t.Stem).ToHashSet(StringComparer.Ordinal);
            if (!stems.Any(have.Contains)) return;
            output.Add(arrow + " " + other.Path + (cited.Contains(other.Path) ? " (citada acima)" : ""));
        }
        foreach (var o in host.Knowledge.Links(d.Id).OrderBy(x => x.Path, StringComparer.Ordinal)) Add(o, "→");
        foreach (var o in host.Knowledge.Backlinks(d.Id).OrderBy(x => x.Path, StringComparer.Ordinal)) Add(o, "←");
        return string.Join(" · ", output);
    }

    // ---------------------------------------------------------------- cite_check

    public enum CiteState { Intact, Present, Moved, Changed, Gone }

    /// <summary>Where the passage is now (<see cref="OtherNote"/>: moved out of the cited note), or nothing.</summary>
    public sealed record CiteResult(CiteState State, string? Path, int From, int To, string? Revision, string? Nearest = null, bool OtherNote = false);

    /// <summary>Where <paramref name="passage"/> is now, against the revision and line it was cited at.</summary>
    public static CiteResult Verify(IAssistantHost host, string path, string passage, string? revision, int? startLine)
    {
        passage = passage.Replace("\r\n", "\n", StringComparison.Ordinal).Trim('\n');
        if (passage.Trim().Length == 0) throw new AiToolException("Trecho vazio.");
        var cited = revision is null ? null : RevisionRegex().Match(revision.ToLowerInvariant()) is { Success: true } rm ? rm.Value : null;
        var d = AssistantTools.ResolveDoc(host, path, must: false);
        if (d is not null && Eligible(d.Path))
        {
            var readable = ReadableText(host, d);
            var now = RevisionOf(readable);
            var masked = Masked(readable);
            var hits = Occurrences(masked, passage);
            if (hits.Count > 0)
            {
                var (from, to) = startLine is { } s && hits.Any(h => h.From == s) ? hits.First(h => h.From == s) : hits[0];
                if (cited == now) return new(CiteState.Intact, d.Path, from, to, now);
                return new(startLine is { } line && line != from ? CiteState.Moved : CiteState.Present, d.Path, from, to, now);
            }
            if (Elsewhere(host, d.Path, passage) is { } moved) return moved;
            if (AssistantTools.Nearest(masked, passage) is { } near) return new(CiteState.Changed, d.Path, near.From, near.To, now, near.Text);
            return new(CiteState.Gone, d.Path, 0, 0, now);
        }
        return Elsewhere(host, null, passage) ?? new(CiteState.Gone, null, 0, 0, null);
    }

    private static CiteResult? Elsewhere(IAssistantHost host, string? except, string passage)
    {
        foreach (var other in host.Documents.List().OrderBy(x => x.Path, StringComparer.Ordinal))
        {
            if (other.Path == except || !Eligible(other.Path)) continue;
            var readable = ReadableText(host, other);
            if (Occurrences(Masked(readable), passage) is [var hit, ..]) return new(CiteState.Moved, other.Path, hit.From, hit.To, RevisionOf(readable), OtherNote: true);
        }
        return null;
    }

    private static List<(int From, int To)> Occurrences(string text, string passage)
    {
        var output = new List<(int, int)>();
        int lines = passage.Count(c => c == '\n');
        for (int at = text.IndexOf(passage, StringComparison.Ordinal); at >= 0; at = text.IndexOf(passage, at + 1, StringComparison.Ordinal))
        {
            int from = 1;
            for (int k = 0; k < at; k++) if (text[k] == '\n') from++;
            output.Add((from, from + lines));
        }
        return output;
    }

    public static string CheckText(IAssistantHost host, string path, string passage, string? revision, int? startLine)
    {
        var r = Verify(host, path, passage, string.IsNullOrWhiteSpace(revision) ? null : revision, startLine);
        string Lines() => r.From == r.To ? "linha " + r.From : "linhas " + r.From + "–" + r.To;
        var p = AssistantTools.Norm(path);
        return r.State switch
        {
            CiteState.Intact => "INTACTO: " + r.Path + " está na mesma revisão (" + r.Revision + "); o trecho está nas " + Lines() + ".",
            CiteState.Present => "PRESENTE: o trecho continua igual em " + r.Path + ", " + Lines() + (revision is null ? "" : "; a nota mudou desde a revisão citada") + " (revisão atual " + r.Revision + ").",
            CiteState.Moved when !r.OtherNote =>
                "MOVIDO na nota: o trecho continua igual em " + r.Path + ", agora nas " + Lines() + (startLine is { } s ? " (antes começava na linha " + s + ")" : "") + " · revisão " + r.Revision + ".",
            CiteState.Moved => "MOVIDO para outra nota: o trecho não está em " + p + ", mas está igual em " + r.Path + ", " + Lines() + " · revisão " + r.Revision + ". Cite a nota nova.",
            CiteState.Changed => "ALTERADO: o trecho não está mais igual em " + r.Path + ". O mais parecido agora (" + Lines() + ", revisão " + r.Revision + "):\n\"\"\"\n" + AssistantTools.Clip(r.Nearest!, 1500) + "\n\"\"\"\nNão reutilize a evidência antiga; confira o texto novo ou busque de novo com find_evidence.",
            _ => r.Path is null
                ? "SUMIU: não há nota " + p + " e o trecho não aparece em nenhuma outra nota. Não reutilize a evidência; busque de novo com find_evidence."
                : "SUMIU: o trecho não está mais em " + r.Path + " (revisão " + r.Revision + ") nem em outra nota. Não reutilize a evidência; busque de novo com find_evidence."
        };
    }

    // ---------------------------------------------------------------- helpers

    private static JsonObject Schema(string json) => (JsonObject)JsonNode.Parse(json)!;
    private static string Str(JsonObject i, string k) => i[k] is { } v ? LegacyCustomize.JsToString(v) : "";
    private static int? Int(JsonObject i, string k) => i[k] is { } v && double.IsFinite(LegacyCustomize.JsToNumber(v)) ? (int)LegacyCustomize.JsToNumber(v) : null;
    private static string Short(string s) => s.Length > 50 ? s[..50] + "…" : s;

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"^(#{1,6})\s+(.*)$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"[0-9a-f]{12}")]
    private static partial Regex RevisionRegex();

    // the same marks NoteMarks.StripComments leaves out of pages: %%comment%%, {"note"}, {ia:"model"}, and &Nome&: requests
    [GeneratedRegex(@"%%(.+?)%%|(?:\{""(?:[^""\\\n]|\\.)*""\})+|\{ia:""[^""\n]{1,80}""\}", RegexOptions.Singleline)]
    private static partial Regex MarksRegex();

    [GeneratedRegex(@"^[ \t]*&((?:\p{L}[\p{L}\p{N} ._-]{0,23})?)&:[ \t]?(.*)$", RegexOptions.Multiline)]
    private static partial Regex AgentLineRegex();

    [GeneratedRegex(@"```[\s\S]*?(?:```|$)|`[^`\n]*`")]
    private static partial Regex CodeRegex();
}
