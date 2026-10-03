using System.Globalization;
using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Extensions.Tables;

namespace Hub.Core;

public enum ReleaseNoteKind { FreeText, Highlights, Improvements, Fixes, BreakingChanges, Technical }
public sealed record ReleaseNoteSection(ReleaseNoteKind Kind, string Heading, string Markdown);
public sealed record ParsedReleaseNotes(string Markdown, IReadOnlyList<ReleaseNoteSection> Sections, bool Structured);

public static class ReleaseNotesParser
{
    internal static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePipeTables().UseAutoLinks().Build();
    public static ParsedReleaseNotes Parse(string? body)
    {
        body ??= "";
        var headings = Markdown.Parse(body, Pipeline).OfType<HeadingBlock>().ToList();
        if (!headings.Any(h => Category(Heading(h)) != ReleaseNoteKind.FreeText))
            return new(body, string.IsNullOrWhiteSpace(body) ? [] : [new(ReleaseNoteKind.FreeText, "", body)], false);
        var sections = new List<ReleaseNoteSection>();
        int start = 0; int level = 7; string title = ""; var kind = ReleaseNoteKind.FreeText;
        foreach (var heading in headings)
        {
            var nextTitle = Heading(heading); var nextKind = Category(nextTitle);
            if (nextKind == ReleaseNoteKind.FreeText && (kind == ReleaseNoteKind.FreeText || heading.Level > level)) continue;
            var content = body[start..heading.Span.Start];
            if (!string.IsNullOrWhiteSpace(content)) sections.Add(new(kind, title, content));
            kind = nextKind; title = nextTitle; level = heading.Level;
            start = Math.Min(body.Length, heading.Span.End + 1);
        }
        if (start < body.Length && !string.IsNullOrWhiteSpace(body[start..])) sections.Add(new(kind, title, body[start..]));
        return new(body, sections, true);
    }

    static string Heading(HeadingBlock heading) => string.Concat(MarkdownPresentation.Inlines(heading.Inline).Select(r => r.Text));
    static ReleaseNoteKind Category(string heading)
    {
        var normalized = new string(heading.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray())
            .Trim().TrimEnd(':').ToLowerInvariant();
        return normalized switch
        {
            "added" or "new" or "novidades" or "adicionado" or "adicionados" or "destaques" or "highlights" or "features" => ReleaseNoteKind.Highlights,
            "changed" or "improved" or "improvements" or "melhorias" or "alteracoes" or "alterado" => ReleaseNoteKind.Improvements,
            "fixed" or "fixes" or "bug fixes" or "correcoes" or "corrigido" => ReleaseNoteKind.Fixes,
            "breaking changes" or "incompatibilidades" or "mudancas incompativeis" => ReleaseNoteKind.BreakingChanges,
            "technical" or "technical details" or "detalhes tecnicos" or "commits" or "pull requests" or "testes" or "tests" or "migracoes" => ReleaseNoteKind.Technical,
            _ => ReleaseNoteKind.FreeText,
        };
    }
}

public sealed record MarkdownRun(string Text, bool Bold = false, bool Italic = false, bool Code = false, string? Url = null);
public sealed record MarkdownBlockView(string Style, IReadOnlyList<MarkdownRun> Runs, int Level = 0)
{ public string Text => string.Concat(Runs.Select(r => r.Text)); }

/// <summary>Apresentação Markdown nativa e testável. Nunca executa HTML nem carrega imagem/recurso remoto.</summary>
public static class MarkdownPresentation
{
    public static IReadOnlyList<MarkdownBlockView> Build(string? markdown)
    {
        markdown ??= "";
        var result = new List<MarkdownBlockView>();
        Blocks(Markdown.Parse(markdown, ReleaseNotesParser.Pipeline), result, markdown, 0);
        return result;
    }

    static void Blocks(ContainerBlock container, List<MarkdownBlockView> output, string source, int level)
    {
        foreach (var block in container)
            switch (block)
            {
                case HeadingBlock heading: output.Add(new("heading", Inlines(heading.Inline), heading.Level)); break;
                case ParagraphBlock paragraph: output.Add(new("paragraph", Inlines(paragraph.Inline), level)); break;
                case CodeBlock code: output.Add(new("code", [new(code.Lines.ToString(), Code: true)], level)); break;
                case ListBlock list:
                    int number = int.TryParse(list.OrderedStart, out var start) ? start : 1;
                    foreach (var item in list.OfType<ListItemBlock>())
                    {
                        var rows = new List<MarkdownBlockView>(); Blocks(item, rows, source, level + 1);
                        if (rows.Count > 0)
                            rows[0] = rows[0] with { Runs = new[] { new MarkdownRun(list.IsOrdered ? $"{number++}. " : "• ") }.Concat(rows[0].Runs).ToList() };
                        output.AddRange(rows);
                    }
                    break;
                case QuoteBlock quote:
                    var quotes = new List<MarkdownBlockView>(); Blocks(quote, quotes, source, level + 1);
                    output.AddRange(quotes.Select(q => q with { Style = q.Style == "code" ? "code" : "quote" }));
                    break;
                case Table table:
                    foreach (var row in table.OfType<TableRow>())
                    {
                        var runs = new List<MarkdownRun>();
                        foreach (var cell in row.OfType<TableCell>())
                        {
                            if (runs.Count > 0) runs.Add(new(" · "));
                            var cells = new List<MarkdownBlockView>(); Blocks(cell, cells, source, level);
                            foreach (var r in cells.SelectMany(c => c.Runs)) runs.Add(row.IsHeader ? r with { Bold = true } : r);
                        }
                        output.Add(new("paragraph", runs, level));
                    }
                    break;
                case ThematicBreakBlock: output.Add(new("rule", [new("────")])); break;
                case ContainerBlock nested: Blocks(nested, output, source, level); break;
                default:
                    int a = Math.Clamp(block.Span.Start, 0, source.Length), b = Math.Clamp(block.Span.End + 1, a, source.Length);
                    output.Add(new("literal", [new(source[a..b])], level)); break;
            }
    }

    internal static IReadOnlyList<MarkdownRun> Inlines(ContainerInline? container, bool bold = false, bool italic = false, string? url = null)
    {
        var result = new List<MarkdownRun>();
        if (container is null) return result;
        foreach (var inline in container)
            switch (inline)
            {
                case LiteralInline literal: result.Add(new(literal.Content.ToString(), bold, italic, Url: url)); break;
                case CodeInline code: result.Add(new(code.Content, bold, italic, true, url)); break;
                case LineBreakInline line: result.Add(new(line.IsHard ? "\n" : " ")); break;
                case EmphasisInline emphasis:
                    result.AddRange(Inlines(emphasis, bold || emphasis.DelimiterCount >= 2, italic || emphasis.DelimiterCount == 1, url)); break;
                case LinkInline link:
                    if (link.IsImage) result.Add(new("[imagem: "));
                    result.AddRange(Inlines(link, bold, italic, link.IsImage ? null : SafeLink(link.Url)));
                    if (link.IsImage) result.Add(new("]"));
                    break;
                case AutolinkInline auto: result.Add(new(auto.Url, bold, italic, Url: SafeLink(auto.Url))); break;
                case HtmlEntityInline entity: result.Add(new(entity.Transcoded.ToString(), bold, italic, Url: url)); break;
                case HtmlInline html: result.Add(new(html.Tag, bold, italic)); break;
                case ContainerInline nested: result.AddRange(Inlines(nested, bold, italic, url)); break;
            }
        return result;
    }

    public static string? SafeLink(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo) ? uri.AbsoluteUri : null;
}
