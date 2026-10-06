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

        var body = RenderBody(page, documents);
        var title = MarkdownEngine.EscapeHtml(page.Title);
        var lang = MarkdownEngine.EscapeHtml(page.Language);

        return "<!doctype html><html lang='" + lang +
               "'><head><meta charset='utf-8'><meta name='viewport' " +
               "content='width=device-width,initial-scale=1'><title>" +
               title + "</title></head><body><main>" + body +
               "</main></body></html>";
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

        if (context.Anchor.Length == 0)
            return html;

        var id = context.Section.Id ?? "s" + (context.Index + 1);
        return "<section data-s='" +
               MarkdownEngine.EscapeHtml(id) +
               "' id='" +
               MarkdownEngine.EscapeHtml(context.Anchor) +
               "'>" +
               html +
               "</section>";
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
