using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Urbe.Core;

internal static class PageBookRenderer
{
    private static readonly string[] NumberWords =
    [
        "zero", "um", "dois", "três", "quatro", "cinco", "seis", "sete",
        "oito", "nove", "dez", "onze", "doze", "treze", "catorze",
        "quinze", "dezesseis", "dezessete", "dezoito", "dezenove", "vinte"
    ];

    public static string Render(
        string type,
        JsonObject props,
        DocumentStore? documents,
        PageRenderPlan plan,
        PageSectionRenderContext context)
    {
        return type switch
        {
            "toc" => RenderToc(props, plan),
            "bookcover" => RenderBookCover(props),
            "titlepage" => RenderTitlePage(props),
            "booktoc" => RenderBookToc(props, plan),
            "part" => RenderPart(props, context),
            "chapter" => RenderChapter(props, documents, plan, context),
            "chapters" => RenderChapters(props, plan, context),
            _ => string.Empty
        };
    }

    public static bool Handles(string type) =>
        type is "toc" or "bookcover" or "titlepage" or "booktoc" or
            "part" or "chapter" or "chapters";

    private static string RenderToc(JsonObject props, PageRenderPlan plan)
    {
        var title = Text(props["title"]) ?? "Nesta página";
        var output = new StringBuilder("<nav class='toc'><strong>")
            .Append(MarkdownEngine.EscapeHtml(title))
            .Append("</strong><ol>");

        foreach (var entry in plan.TocEntries)
        {
            output.Append("<li><a href='#")
                .Append(MarkdownEngine.EscapeHtml(entry.Anchor))
                .Append("'>")
                .Append(MarkdownEngine.EscapeHtml(entry.Title))
                .Append("</a></li>");
        }

        output.Append("</ol></nav>");
        return output.ToString();
    }

    private static string RenderBookCover(JsonObject props)
    {
        var title = Text(props["title"]) ?? "O título do livro";
        var subtitle = Text(props["subtitle"]) ?? string.Empty;
        var author = Text(props["author"]) ?? string.Empty;
        var publisher = Text(props["publisher"]) ?? string.Empty;
        var style = CssToken(Text(props["style"]) ?? "classic");
        var image = SafeImage(Text(props["image"]));

        var output = new StringBuilder("<div class='sheet bk-full bk-cover s-")
            .Append(style)
            .Append("'");

        if (image.Length > 0 && style == "image")
        {
            output.Append(" style='background-image:url(&quot;")
                .Append(MarkdownEngine.EscapeHtml(image))
                .Append("&quot;)'");
        }

        output.Append("><div class='bk-cover-in'>");
        if (style == "modern")
            output.Append("<span class='bk-bar'></span>");

        output.Append("<div class='bk-cover-top'><h1>")
            .Append(MarkdownEngine.InlineMarkdown(title))
            .Append("</h1>");

        if (subtitle.Length > 0)
        {
            output.Append("<p class='bk-cover-sub'>")
                .Append(MarkdownEngine.InlineMarkdown(subtitle))
                .Append("</p>");
        }

        output.Append("</div>");

        if (image.Length > 0 && style != "image")
        {
            output.Append("<figure class='bk-cover-img'><img src='")
                .Append(MarkdownEngine.EscapeHtml(image))
                .Append("' alt='")
                .Append(MarkdownEngine.EscapeHtml(title))
                .Append("'></figure>");
        }

        output.Append("<div class='bk-cover-bottom'>");
        if (author.Length > 0)
        {
            output.Append("<p class='bk-author'>")
                .Append(MarkdownEngine.EscapeHtml(author))
                .Append("</p>");
        }
        if (publisher.Length > 0)
        {
            output.Append("<p class='bk-pub'>")
                .Append(MarkdownEngine.EscapeHtml(publisher))
                .Append("</p>");
        }

        output.Append("</div></div></div>");
        return output.ToString();
    }

    private static string RenderTitlePage(JsonObject props)
    {
        var title = Text(props["title"]) ?? "O título do livro";
        var subtitle = Text(props["subtitle"]) ?? string.Empty;
        var author = Text(props["author"]) ?? string.Empty;
        var publisher = Text(props["publisher"]) ?? string.Empty;
        var place = Text(props["place"]) ?? string.Empty;
        var year = Text(props["year"]) ?? string.Empty;

        var output = new StringBuilder(
            "<div class='sheet bk-full bk-front bk-title'><div class='bk-title-top'><p class='bk-author'>")
            .Append(MarkdownEngine.EscapeHtml(author))
            .Append("</p></div><div class='bk-title-mid'><h1>")
            .Append(MarkdownEngine.InlineMarkdown(title))
            .Append("</h1>");

        if (subtitle.Length > 0)
        {
            output.Append("<p class='bk-title-sub'>")
                .Append(MarkdownEngine.InlineMarkdown(subtitle))
                .Append("</p>");
        }

        output.Append("<span class='bk-orn'>❦</span></div><div class='bk-title-bottom'>");
        if (publisher.Length > 0)
        {
            output.Append("<p class='bk-pub'>")
                .Append(MarkdownEngine.EscapeHtml(publisher))
                .Append("</p>");
        }

        var placeYear = string.Join(
            " · ",
            new[] { place, year }.Where(value => value.Length > 0));
        if (placeYear.Length > 0)
        {
            output.Append("<p class='bk-place'>")
                .Append(MarkdownEngine.EscapeHtml(placeYear))
                .Append("</p>");
        }

        output.Append("</div></div>");
        return output.ToString();
    }

    private static string RenderBookToc(JsonObject props, PageRenderPlan plan)
    {
        var title = Text(props["title"]) ?? "Sumário";
        var output = new StringBuilder("<div class='sheet bk-front bk-toc'><h2>")
            .Append(MarkdownEngine.EscapeHtml(title))
            .Append("</h2>");

        if (plan.BookEntries.Count == 0)
        {
            output.Append("<p class='missing'>Adicione capítulos para o sumário aparecer.</p></div>");
            return output.ToString();
        }

        output.Append("<ol>");
        foreach (var entry in plan.BookEntries)
        {
            var label = entry.Kind == "part"
                ? "Parte " + Roman(entry.Number)
                : ChapterLabel(
                    entry.Number,
                    plan.ChapterStyle == "words"
                        ? "number"
                        : plan.ChapterStyle);

            output.Append("<li class='k-")
                .Append(CssToken(entry.Kind))
                .Append("'><a href='#")
                .Append(MarkdownEngine.EscapeHtml(entry.Anchor))
                .Append("'>");

            if (label.Length > 0)
            {
                output.Append("<span class='n'>")
                    .Append(
                        MarkdownEngine.EscapeHtml(
                            label.StartsWith(
                                "Capítulo ",
                                StringComparison.Ordinal)
                                ? label["Capítulo ".Length..]
                                : label))
                    .Append("</span>");
            }

            output.Append("<span class='t'>")
                .Append(MarkdownEngine.InlineMarkdown(entry.Title))
                .Append("</span><span class='lead' aria-hidden='true'></span></a></li>");
        }

        output.Append("</ol></div>");
        return output.ToString();
    }

    private static string RenderPart(
        JsonObject props,
        PageSectionRenderContext context)
    {
        var title = Text(props["title"]) ?? "Título da parte";
        var markdown = Text(props["markdown"]) ?? string.Empty;
        var output = new StringBuilder("<div class='sheet bk-full bk-part'");

        if (context.Anchor.Length > 0)
        {
            output.Append(" id='")
                .Append(MarkdownEngine.EscapeHtml(context.Anchor))
                .Append("'");
        }

        output.Append("><p class='bk-partnum'>Parte ")
            .Append(MarkdownEngine.EscapeHtml(Roman(Math.Max(1, context.PartNumber))))
            .Append("</p><h2>")
            .Append(MarkdownEngine.InlineMarkdown(title))
            .Append("</h2>");

        if (markdown.Length > 0)
        {
            output.Append("<div class='prose'>")
                .Append(MarkdownEngine.Render(markdown))
                .Append("</div>");
        }

        output.Append("</div>");
        return output.ToString();
    }

    private static string RenderChapter(
        JsonObject props,
        DocumentStore? documents,
        PageRenderPlan plan,
        PageSectionRenderContext context)
    {
        var title = Text(props["title"]) ?? string.Empty;
        string markdown;

        if (string.Equals(
                Text(props["source"]) ?? "text",
                "note",
                StringComparison.Ordinal))
        {
            var path = Text(props["path"]) ?? string.Empty;
            var document = PageRenderPlan.FindDocument(documents, path);
            if (document is null)
            {
                return "<div class='sheet bk-chap'><div class='missing'>Nota não encontrada: " +
                       MarkdownEngine.EscapeHtml(
                           path.Length == 0 ? "(escolha uma nota)" : path) +
                       "</div></div>";
            }

            if (title.Length == 0)
                title = document.Title;
            markdown = StripLeadingTitle(
                document.Content,
                document.Title);
        }
        else
        {
            markdown = Text(props["markdown"]) ?? string.Empty;
        }

        return ChapterHtml(
            plan,
            context.ChapterNumber,
            context.Anchor,
            title,
            Text(props["epigraph"]) ?? string.Empty,
            Text(props["epigraphAuthor"]) ?? string.Empty,
            Bool(props["dropCap"], true),
            MarkdownEngine.Render(ShiftHeadings(markdown, 2)));
    }

    private static string RenderChapters(
        JsonObject props,
        PageRenderPlan plan,
        PageSectionRenderContext context)
    {
        if (context.ChapterDocuments.Count == 0)
        {
            return "<div class='sheet bk-chap'><div class='missing'>Nenhuma nota na pasta " +
                   MarkdownEngine.EscapeHtml(
                       Text(props["folder"]) is { Length: > 0 } folder
                           ? folder
                           : "(escolha uma pasta)") +
                   ".</div></div>";
        }

        var dropCap = Bool(props["dropCap"], true);
        return string.Concat(
            context.ChapterDocuments.Select(chapter =>
                ChapterHtml(
                    plan,
                    chapter.Number,
                    chapter.Anchor,
                    chapter.Document.Title,
                    string.Empty,
                    string.Empty,
                    dropCap,
                    MarkdownEngine.Render(
                        ShiftHeadings(
                            StripLeadingTitle(
                                chapter.Document.Content,
                                chapter.Document.Title),
                            2)))));
    }

    private static string ChapterHtml(
        PageRenderPlan plan,
        int number,
        string anchor,
        string title,
        string epigraph,
        string epigraphAuthor,
        bool dropCap,
        string body)
    {
        var label = ChapterLabel(number, plan.ChapterStyle);
        var output = new StringBuilder("<article class='sheet bk-chap")
            .Append(dropCap ? " dropcap" : string.Empty)
            .Append("'");

        if (anchor.Length > 0)
        {
            output.Append(" id='")
                .Append(MarkdownEngine.EscapeHtml(anchor))
                .Append("'");
        }

        output.Append("><header class='bk-chhead'>");
        if (label.Length > 0)
        {
            output.Append("<p class='bk-chnum'>")
                .Append(MarkdownEngine.EscapeHtml(label))
                .Append("</p>");
        }
        if (title.Length > 0)
        {
            output.Append("<h2 class='bk-chtitle'>")
                .Append(MarkdownEngine.InlineMarkdown(title))
                .Append("</h2>");
        }
        if (epigraph.Length > 0)
        {
            output.Append("<blockquote class='bk-epi'>")
                .Append(MarkdownEngine.Render(epigraph));
            if (epigraphAuthor.Length > 0)
            {
                output.Append("<cite>— ")
                    .Append(MarkdownEngine.InlineMarkdown(epigraphAuthor))
                    .Append("</cite>");
            }
            output.Append("</blockquote>");
        }

        output.Append("</header><div class='prose bk-text'>")
            .Append(body)
            .Append("</div></article>");
        return output.ToString();
    }

    private static string ChapterLabel(int number, string style)
    {
        if (number <= 0 || style == "none")
            return string.Empty;
        if (style == "number")
            return number.ToString(CultureInfo.InvariantCulture);
        if (style == "roman")
            return Roman(number);
        if (style == "words")
        {
            return "Capítulo " +
                   (number < NumberWords.Length
                       ? NumberWords[number]
                       : number.ToString(CultureInfo.InvariantCulture));
        }

        return "Capítulo " + number.ToString(CultureInfo.InvariantCulture);
    }

    private static string Roman(int number)
    {
        var n = Math.Max(1, number);
        var values = new (int Value, string Symbol)[]
        {
            (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"),
            (100, "C"), (90, "XC"), (50, "L"), (40, "XL"),
            (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
        };
        var output = new StringBuilder();
        foreach (var (value, symbol) in values)
        {
            while (n >= value)
            {
                output.Append(symbol);
                n -= value;
            }
        }
        return output.ToString();
    }

    private static string ShiftHeadings(string markdown, int shift)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        var inFence = false;
        var fence = new string((char)96, 3);

        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].StartsWith(fence, StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }

            if (inFence)
                continue;

            var count = 0;
            while (count < lines[index].Length &&
                   count < 6 &&
                   lines[index][count] == '#')
            {
                count++;
            }

            if (count == 0 ||
                count >= lines[index].Length ||
                lines[index][count] != ' ')
            {
                continue;
            }

            var target = Math.Min(6, count + shift);
            lines[index] = new string('#', target) + lines[index][count..];
        }

        return string.Join("\n", lines);
    }

    private static string StripLeadingTitle(string content, string title)
    {
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = normalized.Split('\n').ToList();

        var start = 0;
        if (lines.Count > 0 && lines[0] == "---")
        {
            var close = lines.FindIndex(1, line => line == "---");
            if (close >= 0)
                start = close + 1;
        }

        while (start < lines.Count && lines[start].Length == 0)
            start++;

        if (start < lines.Count &&
            lines[start].StartsWith("# ", StringComparison.Ordinal) &&
            string.Equals(
                lines[start][2..].Trim(),
                title.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            lines.RemoveAt(start);
        }

        return string.Join("\n", lines);
    }

    private static string? Text(JsonNode? node) =>
        PageDocument.StringValue(node);

    private static bool Bool(JsonNode? node, bool fallback) =>
        node is null ? fallback : PageDocument.BoolValue(node, fallback);

    private static string SafeImage(string? value)
    {
        var safe = MarkdownEngine.SafeUrl(value);
        return string.IsNullOrWhiteSpace(safe) || safe == "#"
            ? string.Empty
            : safe;
    }

    private static string CssToken(string? value)
    {
        var source = value ?? string.Empty;
        var chars = source
            .Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_')
            .ToArray();
        return chars.Length == 0 ? "default" : new string(chars);
    }
}
