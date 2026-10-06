using System.Text;
using System.Text.Json.Nodes;

namespace Urbe.Core;

/// <summary>
/// Pure semantic renderer for persisted pages. This is deliberately UI-free:
/// hosts decide preview/WebView concerns later. Unknown blocks are preserved in
/// PageDocument and omitted from this renderer until their C# port lands.
/// </summary>
public static class PageRenderer
{
    public static string Render(PageDocument page, DocumentStore? documents = null)
    {
        ArgumentNullException.ThrowIfNull(page);

        var normalized = page.IsReadOnly
            ? page
            : PageNormalizer.Normalize(page).Page;
        var raw = normalized.Raw;
        var meta = raw["meta"] as JsonObject ?? new JsonObject();
        var theme = raw["theme"] as JsonObject ?? new JsonObject();
        var layout = raw["layout"] as JsonObject ?? new JsonObject();
        var plan = PageRenderPlan.Create(normalized, documents);
        var body = RenderBody(normalized, documents);
        var title = MarkdownEngine.EscapeHtml(normalized.Title);
        var lang = MarkdownEngine.EscapeHtml(normalized.Language);
        var description = PageDocument.StringValue(meta["description"]) ?? string.Empty;
        var head = SafeHead(PageDocument.StringValue(meta["head"]));
        var resolvedTheme = PageThemeCatalog.Resolve(theme);
        var modeClass = resolvedTheme.Mode switch
        {
            "dark" => "dark-default",
            "light" => "light-default",
            _ => string.Empty
        };
        var htmlClass = string.Join(
            " ",
            new[] { modeClass, plan.BookFormat ? "book" : string.Empty }
                .Where(value => value.Length > 0));
        var fonts = PageThemeCatalog.GoogleFontsQuery(resolvedTheme);
        var css = PageThemeCatalog.BuildCss(theme, layout, plan.BookFormat);

        var nav = string.Empty;
        if (!plan.BookFormat && PageDocument.BoolValue(layout["nav"], true))
        {
            var brand = PageDocument.StringValue(layout["brand"]) ?? normalized.Title;
            var menu = string.Concat(
                plan.TocEntries
                    .Where(entry => entry.Menu)
                    .Select(entry =>
                        "<a href='#" + MarkdownEngine.EscapeHtml(entry.Anchor) + "'>" +
                        MarkdownEngine.EscapeHtml(entry.Title) + "</a>"));
            var cta = PageDocument.StringValue(layout["navCta"]) ?? string.Empty;
            var ctaUrl = MarkdownEngine.SafeUrl(
                PageDocument.StringValue(layout["navCtaUrl"]) ?? "#");
            if (cta.Length > 0)
            {
                menu += "<a class='btn' href='" +
                        MarkdownEngine.EscapeHtml(ctaUrl) + "'>" +
                        MarkdownEngine.EscapeHtml(cta) + "</a>";
            }

            nav = "<header class='nav" +
                  (PageDocument.BoolValue(layout["sticky"], true) ? " sticky" : "") +
                  "'><div class='wrap'><a class='brand' href='#top'>" +
                  MarkdownEngine.EscapeHtml(brand) +
                  "</a><nav class='nav-links'>" + menu +
                  "</nav></div></header>";
        }

        var footerMarkdown = plan.BookFormat
            ? string.Empty
            : PageDocument.StringValue(layout["footer"]) ?? string.Empty;
        var footer = footerMarkdown.Length > 0
            ? "<footer class='foot'><div class='wrap'>" +
              MarkdownEngine.Render(footerMarkdown) + "</div></footer>"
            : string.Empty;
        var progress = !plan.BookFormat &&
                       PageDocument.BoolValue(layout["progress"])
            ? "<div class='progress' aria-hidden='true'></div>"
            : string.Empty;
        var themeToggle = !plan.BookFormat &&
                          PageDocument.BoolValue(layout["themeToggle"], true)
            ? "<button class='fab fab-theme' type='button' aria-label='Alternar tema'>◐</button>"
            : string.Empty;
        var backToTop = !plan.BookFormat &&
                        PageDocument.BoolValue(layout["backToTop"], true)
            ? "<button class='fab fab-top' type='button' aria-label='Voltar ao topo'>↑</button>"
            : string.Empty;
        var print = plan.BookFormat
            ? "<button class='fab fab-print' type='button' onclick='print()'>Imprimir ou salvar PDF</button>"
            : string.Empty;

        var output = new StringBuilder("<!doctype html><html");
        if (htmlClass.Length > 0)
            output.Append(" class='").Append(MarkdownEngine.EscapeHtml(htmlClass)).Append('\'');
        output.Append(" lang='").Append(lang)
            .Append("'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1,viewport-fit=cover'><title>")
            .Append(title).Append("</title>");
        if (description.Length > 0)
        {
            output.Append("<meta name='description' content='")
                .Append(MarkdownEngine.EscapeHtml(description))
                .Append("'><meta property='og:description' content='")
                .Append(MarkdownEngine.EscapeHtml(description)).Append("'>");
        }
        output.Append("<meta property='og:title' content='").Append(title)
            .Append("'><meta name='generator' content='Urbe'>");
        if (fonts.Length > 0)
        {
            output.Append("<link rel='preconnect' href='https://fonts.googleapis.com'><link rel='preconnect' href='https://fonts.gstatic.com' crossorigin><link rel='stylesheet' href='https://fonts.googleapis.com/css2?")
                .Append(MarkdownEngine.EscapeHtml(fonts)).Append("&display=swap'>");
        }
        output.Append(head).Append("<style>").Append(css).Append("</style></head><body id='top'>")
            .Append(progress).Append(nav).Append("<main");
        if (!plan.BookFormat)
            output.Append(" class='wrap'");
        output.Append('>').Append(body).Append("</main>")
            .Append(footer).Append(themeToggle).Append(backToTop).Append(print)
            .Append("</body></html>");
        return output.ToString();
    }

    public static string RenderBody(PageDocument page, DocumentStore? documents = null)
    {
        ArgumentNullException.ThrowIfNull(page);

        var plan = PageRenderPlan.Create(page, documents);
        var output = new StringBuilder();

        foreach (var context in plan.Sections)
        {
            var section = context.Section;
            if (PageDocument.BoolValue(section.Style["hidden"]))
                continue;

            var html = RenderSection(context, documents, plan);
            output.Append(WrapSection(context, html));
        }

        return output.ToString();
    }

    private static string RenderSection(
        PageSectionRenderContext context,
        DocumentStore? documents,
        PageRenderPlan plan)
    {
        var section = context.Section;
        var props = section.Props;

        if (PageBookRenderer.Handles(section.Type))
        {
            return PageBookRenderer.Render(
                section.Type,
                props,
                documents,
                plan,
                context);
        }

        if (section.Type == "free")
        {
            return PageFreeRenderer.Render(
                props,
                documents,
                section.Id ?? "s" + (context.Index + 1),
                plan.BookFormat).Html;
        }

        return section.Type switch
        {
            "hero" => RenderHero(props),
            "text" => RenderText(props),
            "note" => RenderNote(props, documents),
            "notes" => RenderNotes(props, documents),
            "quote" => RenderQuote(props),
            "image" => RenderImage(props),
            _ => PageRichBlockRenderer.Render(section.Type, props, documents)
        };
    }

    private static string WrapSection(
        PageSectionRenderContext context,
        string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        if (context.Section.Type is "part" or "chapter")
            return html;

        var style = context.Section.Style;
        var id = context.Section.Id ?? "s" + (context.Index + 1);
        var classes = new List<string> { "sec" };
        var background = PageDocument.StringValue(style["background"]);
        if (!string.IsNullOrEmpty(background) && background != "none")
            classes.Add("bg-" + CssToken(background));
        var padding = PageDocument.StringValue(style["padding"]);
        if (!string.IsNullOrEmpty(padding) && padding != "none")
            classes.Add("p-" + CssToken(padding));
        var width = PageDocument.StringValue(style["width"]);
        if (!string.IsNullOrEmpty(width) && width != "normal")
            classes.Add("w-" + CssToken(width));
        var align = PageDocument.StringValue(style["align"]);
        if (align == "center")
            classes.Add("center");
        if (PageDocument.BoolValue(style["boxed"]))
            classes.Add("boxed");
        var minHeight = PageDocument.StringValue(style["minHeight"]);
        if (!string.IsNullOrEmpty(minHeight) && minHeight != "none")
            classes.Add("mh-" + CssToken(minHeight));
        var animation = PageDocument.StringValue(style["animation"]);
        if (!string.IsNullOrEmpty(animation) && animation != "none")
            classes.Add("an-" + CssToken(animation));
        var customClass = CleanClasses(PageDocument.StringValue(style["className"]));
        if (customClass.Length > 0)
            classes.Add(customClass);

        var inline = new StringBuilder();
        var bg = PageDocument.StringValue(style["bgColor"]);
        if (!string.IsNullOrEmpty(bg))
            inline.Append("background:").Append(bg).Append(';');
        var text = PageDocument.StringValue(style["textColor"]);
        if (!string.IsNullOrEmpty(text))
            inline.Append("color:").Append(text).Append(';');

        var customCss = PageFreeLayout.CssSafe(PageDocument.StringValue(style["css"]));
        var anchor = context.Anchor.Length > 0
            ? " id='" + MarkdownEngine.EscapeHtml(context.Anchor) + "'"
            : string.Empty;
        var styleAttr = inline.Length > 0
            ? " style='" + MarkdownEngine.EscapeHtml(inline.ToString()) + "'"
            : string.Empty;

        return "<section data-s='" +
               MarkdownEngine.EscapeHtml(id) + "'" + anchor +
               " class='" + MarkdownEngine.EscapeHtml(string.Join(" ", classes)) + "'" +
               styleAttr + ">" +
               (customCss.Length > 0
                   ? "<style>[data-s='" + MarkdownEngine.EscapeHtml(id) + "']{" +
                     customCss.Replace("&", "[data-s='" + MarkdownEngine.EscapeHtml(id) + "']", StringComparison.Ordinal) +
                     "}</style>"
                   : string.Empty) +
               html +
               "</section>";
    }

    private static string SafeHead(string? value) =>
        (value ?? string.Empty)
            .Replace("</head", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("<script", "&lt;script", StringComparison.OrdinalIgnoreCase);

    private static string CssToken(string? value) =>
        new string((value ?? string.Empty)
            .Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_')
            .ToArray());

    private static string CleanClasses(string? value)
    {
        var clean = new string((value ?? string.Empty)
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' || char.IsWhiteSpace(ch) ? ch : ' ')
            .ToArray());
        return string.Join(" ", clean
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(8));
    }

    private static string RenderHero(JsonObject props)
    {
        var title = Text(props["title"]) ?? "Um título que diz tudo";
        var subtitle = Text(props["subtitle"]) ?? string.Empty;
        var eyebrow = Text(props["eyebrow"]) ?? string.Empty;

        return "<section class='hero'>" +
               (eyebrow.Length > 0
                   ? "<p class='eyebrow'>" + MarkdownEngine.InlineMarkdown(eyebrow) + "</p>"
                   : string.Empty) +
               "<h1>" + MarkdownEngine.InlineMarkdown(title) + "</h1>" +
               (subtitle.Length > 0
                   ? "<div class='lead'>" + MarkdownEngine.Render(subtitle) + "</div>"
                   : string.Empty) +
               "</section>";
    }

    private static string RenderText(JsonObject props)
    {
        var title = Text(props["title"]) ?? string.Empty;
        var markdown = Text(props["markdown"]) ?? string.Empty;

        return "<section class='text'>" +
               (title.Length > 0
                   ? "<h2>" + MarkdownEngine.InlineMarkdown(title) + "</h2>"
                   : string.Empty) +
               "<div class='prose'>" + MarkdownEngine.Render(markdown) + "</div>" +
               "</section>";
    }

    private static string RenderNote(JsonObject props, DocumentStore? documents)
    {
        var path = Text(props["path"]) ?? string.Empty;
        var document = FindDocument(documents, path);
        if (document is null)
        {
            return "<section class='note missing'>Nota não encontrada: " +
                   MarkdownEngine.EscapeHtml(path.Length == 0 ? "(escolha uma nota)" : path) +
                   "</section>";
        }

        var showTitle = Bool(props["showTitle"], true);
        var showMeta = Bool(props["showMeta"], false);
        var meta = showMeta
            ? "<p class='meta'>" +
              MarkdownEngine.EscapeHtml(
                  string.Join(
                      " · ",
                      new[]
                      {
                          document.Modified ?? string.Empty,
                          string.Join(" ", document.Tags.Select(tag => "#" + tag))
                      }.Where(value => value.Length > 0))) +
              "</p>"
            : string.Empty;

        return "<article class='prose note-article'>" +
               (showTitle
                   ? "<h1 class='note-title'>" + MarkdownEngine.EscapeHtml(document.Title) + "</h1>"
                   : string.Empty) +
               meta +
               MarkdownEngine.Render(StripLeadingTitle(document.Content, document.Title, showTitle)) +
               "</article>";
    }

    private static string RenderNotes(JsonObject props, DocumentStore? documents)
    {
        var selected = SelectDocuments(props, documents);
        var title = Text(props["title"]) ?? string.Empty;
        var expand = Bool(props["expand"], false);

        var output = new StringBuilder("<section class='notes'>");
        if (title.Length > 0)
            output.Append("<h2>").Append(MarkdownEngine.InlineMarkdown(title)).Append("</h2>");

        if (selected.Count == 0)
        {
            output.Append("<div class='missing'>Nenhuma nota encontrada para esta coleção.</div></section>");
            return output.ToString();
        }

        output.Append("<div class='note-list'>");
        foreach (var document in selected)
        {
            output.Append("<article class='note-card'><h3>")
                .Append(MarkdownEngine.EscapeHtml(document.Title))
                .Append("</h3></article>");
        }
        output.Append("</div>");

        if (expand)
        {
            output.Append("<div class='note-full'>");
            foreach (var document in selected)
            {
                output.Append("<article class='prose note-article'><h2 class='note-title'>")
                    .Append(MarkdownEngine.EscapeHtml(document.Title))
                    .Append("</h2>")
                    .Append(MarkdownEngine.Render(
                        StripLeadingTitle(document.Content, document.Title, true)))
                    .Append("</article>");
            }
            output.Append("</div>");
        }

        output.Append("</section>");
        return output.ToString();
    }

    private static string RenderQuote(JsonObject props)
    {
        var text = Text(props["text"]) ?? string.Empty;
        var author = Text(props["author"]) ?? string.Empty;

        return "<figure class='bigquote'><blockquote>" +
               MarkdownEngine.InlineMarkdown(text) +
               "</blockquote>" +
               (author.Length > 0
                   ? "<figcaption><strong>" + MarkdownEngine.EscapeHtml(author) + "</strong></figcaption>"
                   : string.Empty) +
               "</figure>";
    }

    private static string RenderImage(JsonObject props)
    {
        var source = Text(props["src"]) ?? string.Empty;
        var safe = MarkdownEngine.SafeUrl(source);
        if (safe == "#" || safe.Length == 0)
            return "<div class='missing'>Escolha uma imagem.</div>";

        var alt = Text(props["alt"]) ?? Text(props["caption"]) ?? string.Empty;
        var caption = Text(props["caption"]) ?? string.Empty;

        return "<figure class='figure'><img src='" +
               MarkdownEngine.EscapeHtml(safe) +
               "' alt='" + MarkdownEngine.EscapeHtml(alt) +
               "'>" +
               (caption.Length > 0
                   ? "<figcaption>" + MarkdownEngine.EscapeHtml(caption) + "</figcaption>"
                   : string.Empty) +
               "</figure>";
    }

    private static string RenderChapter(JsonObject props, DocumentStore? documents)
    {
        var source = Text(props["source"]) ?? "text";
        var title = Text(props["title"]) ?? string.Empty;
        string markdown;

        if (source == "note")
        {
            var path = Text(props["path"]) ?? string.Empty;
            var document = FindDocument(documents, path);
            if (document is null)
            {
                return "<section class='chapter missing'>Nota não encontrada: " +
                       MarkdownEngine.EscapeHtml(path) + "</section>";
            }

            if (title.Length == 0)
                title = document.Title;
            markdown = StripLeadingTitle(document.Content, document.Title, true);
        }
        else
        {
            markdown = Text(props["markdown"]) ?? string.Empty;
        }

        return "<section class='chapter'>" +
               (title.Length > 0
                   ? "<h1>" + MarkdownEngine.InlineMarkdown(title) + "</h1>"
                   : string.Empty) +
               MarkdownEngine.Render(markdown) +
               "</section>";
    }

    private static string RenderChapters(JsonObject props, DocumentStore? documents)
    {
        var folder = Text(props["folder"]) ?? string.Empty;
        var selected = SelectDocuments(
            new JsonObject
            {
                ["source"] = "folder",
                ["folder"] = folder,
                ["sort"] = Text(props["sort"]) ?? "path",
                ["limit"] = 200
            },
            documents);

        if (selected.Count == 0)
        {
            return "<section class='chapter missing'>Nenhuma nota na pasta " +
                   MarkdownEngine.EscapeHtml(folder) + ".</section>";
        }

        return string.Concat(
            selected.Select(document =>
                "<section class='chapter'><h1>" +
                MarkdownEngine.EscapeHtml(document.Title) +
                "</h1>" +
                MarkdownEngine.Render(
                    StripLeadingTitle(document.Content, document.Title, true)) +
                "</section>"));
    }

    private static IReadOnlyList<UrbeDocument> SelectDocuments(
        JsonObject props,
        DocumentStore? documents)
    {
        if (documents is null)
            return Array.Empty<UrbeDocument>();

        var all = documents.List().ToList();
        var source = Text(props["source"]) ?? "folder";
        IEnumerable<UrbeDocument> query = all;

        switch (source)
        {
            case "folder":
            {
                var folder = (Text(props["folder"]) ?? string.Empty)
                    .Trim('/')
                    .ToLowerInvariant();
                query = all.Where(document =>
                    folder.Length == 0 ||
                    document.Path.ToLowerInvariant()
                        .StartsWith(folder + "/", StringComparison.Ordinal));
                break;
            }
            case "tag":
            {
                var tag = (Text(props["tag"]) ?? string.Empty).TrimStart('#');
                query = all.Where(document =>
                    document.Tags.Any(existing =>
                        string.Equals(existing, tag, StringComparison.OrdinalIgnoreCase)));
                break;
            }
            case "list":
            {
                var paths = (Text(props["paths"]) ?? string.Empty)
                    .Split(
                        '\n',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries);
                query = paths
                    .Select(path => FindDocument(documents, path))
                    .Where(document => document is not null)
                    .Cast<UrbeDocument>();
                break;
            }
            case "recent":
                query = all.OrderByDescending(document => document.Modified ?? string.Empty);
                break;
        }

        var sort = Text(props["sort"]) ?? "title";
        if (source is not "list" and not "recent")
        {
            query = sort switch
            {
                "modified" => query.OrderByDescending(
                    document => document.Modified ?? string.Empty),
                "path" => query.OrderBy(
                    document => document.Path,
                    StringComparer.Ordinal),
                _ => query.OrderBy(
                    document => document.Title,
                    StringComparer.Ordinal)
            };
        }

        var limit = Math.Clamp(Int(props["limit"], 24), 1, 200);
        return Array.AsReadOnly(query.Take(limit).ToArray());
    }

    private static UrbeDocument? FindDocument(DocumentStore? documents, string path)
    {
        if (documents is null || string.IsNullOrWhiteSpace(path))
            return null;

        var direct = documents.Get(path);
        if (direct is not null)
            return direct;

        if (!path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            direct = documents.Get(path + ".md");
            if (direct is not null)
                return direct;
        }

        var wanted = Path.GetFileNameWithoutExtension(path);
        return documents.List().FirstOrDefault(document =>
            string.Equals(document.Title, wanted, StringComparison.OrdinalIgnoreCase));
    }

    private static string StripLeadingTitle(
        string content,
        string title,
        bool titleShown)
    {
        if (!titleShown)
            return content;

        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
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

    private static int Int(JsonNode? node, int fallback) =>
        PageDocument.IntValue(node) ?? fallback;
}
