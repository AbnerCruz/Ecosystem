using System.Text.RegularExpressions;

namespace Urbe.Core;

public enum ReviewSeverity { Error, Warning }

/// <summary>One thing to fix in a publication: where (section, note, line), what and how.</summary>
public sealed record ReviewIssue(ReviewSeverity Severity, int Section, string? NotePath, int? Line, string Message, string Fix)
{
    public string Place => (NotePath ?? "seção " + (Section + 1)) + (Line is { } line ? ", linha " + line : "");
}

/// <summary>
/// CST-179/180: an automatic review of a publication before it is exported, read from the live notes the way every
/// output reads them: sources that are missing or empty, heading levels that skip (screen readers and the outline),
/// pictures without a description or missing from the vault, links to notes that do not exist, quizzes, calculations and
/// data blocks that fail, slides with more text than fits and video scenes whose subtitles would go by too fast. Each
/// issue names the note and line and says how to fix it; nothing is changed.
/// </summary>
public static partial class PublicationReview
{
    /// <summary>Words a 16:9 slide holds at the deck's reading size before its text no longer fits.</summary>
    public const int SlideWords = 110;
    /// <summary>Reading speed of subtitles (words per second) above which they go by too fast.</summary>
    public const double SubtitleWordsPerSecond = 3.2;

    public static IReadOnlyList<ReviewIssue> Run(PublicationProject project, PublicationSourceCatalog catalog,
        Func<string, bool>? fileExists = null, Func<string, bool>? noteExists = null, Func<string, string?>? readData = null)
    {
        var issues = new List<ReviewIssue>();
        if (project.Sections.Count == 0)
        {
            issues.Add(new(ReviewSeverity.Error, 0, null, null, "A publicação não tem nenhuma nota.", "Adicione notas com “Adicionar nota”."));
            return issues;
        }
        for (int s = 0; s < project.Sections.Count; s++)
        {
            var section = project.Sections[s];
            var source = catalog.Read(section.Source);
            if (source is null)
            {
                issues.Add(new(ReviewSeverity.Error, s, null, null, "A nota desta seção não existe mais ou o trecho indicado não foi encontrado.",
                    "Restaure a nota da lixeira, corrija o trecho ou remova a seção."));
                continue;
            }
            string path = source.Path;
            string body = MarkdownEngine.SplitFrontmatter(source.Markdown).Body;
            // the body's first line is this line of the note (after a frontmatter)
            int offset = Math.Max(0, CountLines(source.Markdown.Replace("\r\n", "\n", StringComparison.Ordinal)) - CountLines(body));
            var text = NoteMarks.StripComments(body);
            if (text.Trim().Length == 0)
            {
                issues.Add(new(ReviewSeverity.Warning, s, path, null, "A nota está vazia: a seção sai em branco.", "Escreva o conteúdo na nota ou remova a seção."));
                continue;
            }
            var lines = body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            int lastLevel = 0;
            bool inFence = false;
            string? fenceLang = null;
            int fenceStart = 0;
            var fence = new List<string>();
            int slideWords = 0, slideStart = 1;
            void CloseSlide(int line)
            {
                if (project.Design.Layout == "slides" && slideWords > SlideWords)
                    issues.Add(new(ReviewSeverity.Warning, s, path, offset + slideStart, "Este slide tem " + slideWords + " palavras: o texto não cabe na tela.",
                        "Divida em dois slides com ## ou ---, ou passe detalhes para :::notas."));
                slideWords = 0; slideStart = line;
            }
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int number = offset + i + 1;
                if (FenceRe().Match(line) is { Success: true } f)
                {
                    if (!inFence) { inFence = true; fenceLang = f.Groups[1].Value.Trim(); fenceStart = number; fence.Clear(); continue; }
                    inFence = false;
                    CheckBlock(fenceLang, string.Join("\n", fence), s, path, fenceStart, issues, readData);
                    continue;
                }
                if (inFence) { fence.Add(line); continue; }
                if (HeadingRe().Match(line) is { Success: true } h)
                {
                    int level = h.Groups[1].Value.Length;
                    if (lastLevel > 0 && level > lastLevel + 1)
                        issues.Add(new(ReviewSeverity.Warning, s, path, number, "O título pula do nível " + lastLevel + " para o " + level + ": leitores de tela e o sumário perdem a estrutura.",
                            "Use " + new string('#', lastLevel + 1) + " neste título."));
                    lastLevel = level;
                    if (level <= 2) CloseSlide(number);
                }
                else if (line.Trim() == "---") CloseSlide(number);
                else slideWords += line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
                foreach (Match image in ImageRe().Matches(line))
                {
                    string alt = image.Groups[1].Value.Trim(), target = image.Groups[2].Value.Trim().Trim('<', '>');
                    if (alt.Length == 0)
                        issues.Add(new(ReviewSeverity.Warning, s, path, number, "Imagem sem descrição: quem usa leitor de tela não sabe o que ela mostra.",
                            "Escreva a descrição entre os colchetes: ![descrição](" + target + ")."));
                    if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        issues.Add(new(ReviewSeverity.Warning, s, path, number, "Imagem da web: não entra no EPUB, no PDF nem nos documentos.",
                            "Salve a imagem no vault e use o caminho dela."));
                    else if (fileExists is not null && !fileExists(Resolve(path, Uri.UnescapeDataString(target.Split(' ')[0]))))
                        issues.Add(new(ReviewSeverity.Error, s, path, number, "Imagem não encontrada no vault: " + target + ".", "Corrija o caminho ou importe a imagem."));
                }
                foreach (Match link in WikiRe().Matches(line))
                {
                    if (link.Index > 0 && line[link.Index - 1] == '!') continue;
                    string target = link.Groups[1].Value.Split('#')[0].Trim();
                    if (target.Length > 0 && noteExists is not null && !noteExists(target))
                        issues.Add(new(ReviewSeverity.Warning, s, path, number, "Link para uma nota que não existe: [[" + target + "]].",
                            "Crie a nota, corrija o nome ou tire os colchetes."));
                }
            }
            if (inFence) issues.Add(new(ReviewSeverity.Error, s, path, fenceStart, "Bloco de código aberto com ``` e nunca fechado: o resto da nota vira código.", "Feche o bloco com ```."));
            CloseSlide(lines.Length + 1);
            if (project.Design.Layout == "video" && project.Video?.Scenes.FirstOrDefault(c => c.PlacementId == section.Id) is { } scene)
            {
                int words = VideoScript.Plain(text).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
                if (scene.Duration > 0 && words / scene.Duration > SubtitleWordsPerSecond)
                    issues.Add(new(ReviewSeverity.Warning, s, path, null,
                        "A cena dura " + scene.Duration.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) + " s para " + words + " palavras: a legenda passa rápido demais para ler.",
                        "Aumente a duração da cena para pelo menos " + Math.Ceiling(words / SubtitleWordsPerSecond) + " s ou encurte o roteiro."));
            }
        }
        return issues.OrderBy(i => i.Severity).ThenBy(i => i.Section).ThenBy(i => i.Line ?? 0).ToArray();
    }

    private static void CheckBlock(string? language, string block, int section, string path, int line, List<ReviewIssue> issues, Func<string, string?>? readData)
    {
        if (QuizBlock.IsLanguage(language))
        {
            var problems = new List<string>();
            var questions = QuizBlock.Parse(block, problems);
            foreach (var problem in problems) issues.Add(new(ReviewSeverity.Error, section, path, line, "Questionário: " + problem, "Corrija a questão no bloco ```quiz."));
            if (questions.Count == 0 && problems.Count == 0) issues.Add(new(ReviewSeverity.Error, section, path, line, "Questionário sem questões.", "Comece cada questão com “? ” e marque a certa com [x]."));
        }
        else if (Calculus.Calculator.IsLanguage(language))
        {
            try
            {
                foreach (var result in Calculus.Calculator.Run(block).Where(r => !r.Ok))
                    issues.Add(new(ReviewSeverity.Error, section, path, line, "Cálculo com erro em “" + result.Input + "”: " + result.Error, "Corrija a linha no bloco ```calculo."));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { issues.Add(new(ReviewSeverity.Error, section, path, line, "Cálculo com erro: " + ex.Message, "Corrija o bloco ```calculo.")); }
        }
        else if (Data.DataBlock.IsLanguage(language) && readData is not null)
        {
            try { Data.DataBlock.Run(block, readData); }
            catch (Exception ex) when (ex is InvalidDataException or FormatException or ArgumentException or InvalidOperationException)
            { issues.Add(new(ReviewSeverity.Error, section, path, line, "Dados indisponíveis: " + ex.Message, "Corrija a fonte ou as etapas do bloco ```dados.")); }
        }
    }

    private static int CountLines(string text) => text.Count(c => c == '\n');

    private static string Resolve(string notePath, string relative)
    {
        var parts = new List<string>((Path.GetDirectoryName(notePath)?.Replace('\\', '/') ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries));
        foreach (var part in relative.Split('/'))
        {
            if (part is "" or ".") continue;
            if (part == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); }
            else parts.Add(part);
        }
        return string.Join('/', parts);
    }

    [GeneratedRegex(@"^\s{0,3}(?:```|~~~)\s*([^`\s]*)")]
    private static partial Regex FenceRe();
    [GeneratedRegex(@"^\s{0,3}(#{1,6})\s+\S")]
    private static partial Regex HeadingRe();
    [GeneratedRegex(@"!\[([^\]]*)\]\(([^)]+)\)")]
    private static partial Regex ImageRe();
    [GeneratedRegex(@"\[\[([^\]|]+)(?:\|[^\]]*)?\]\]")]
    private static partial Regex WikiRe();
}
