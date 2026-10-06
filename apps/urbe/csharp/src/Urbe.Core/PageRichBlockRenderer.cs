using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// Semantic renderer for page blocks that do not need host/UI state. It mirrors
/// the legacy JS block meanings while keeping URLs escaped/safe and all data
/// access inside Urbe.Core.
/// </summary>
internal static partial class PageRichBlockRenderer
{
    public static string Render(
        string type,
        JsonObject props,
        DocumentStore? documents)
    {
        return type switch
        {
            "features" => RenderFeatures(props),
            "cards" => RenderCards(props),
            "gallery" => RenderGallery(props),
            "video" => RenderVideo(props),
            "testimonials" => RenderTestimonials(props),
            "stats" => RenderStats(props),
            "timeline" => RenderTimeline(props),
            "faq" => RenderFaq(props),
            "cta" => RenderCta(props),
            "columns" => RenderColumns(props),
            "pricing" => RenderPricing(props),
            "contact" => RenderContact(props),
            "countdown" => RenderCountdown(props),
            "code" => RenderCode(props),
            "divider" => RenderDivider(props),
            "html" => Text(props["code"]) ?? string.Empty,
            "about" => RenderAbout(props),
            "copyright" => RenderCopyright(props),
            "dedication" => RenderDedication(props),
            "colophon" => RenderColophon(props),
            _ => string.Empty
        };
    }

    private static string RenderFeatures(JsonObject props)
    {
        var output = new StringBuilder(Head(props));
        output.Append("<div class='grid features'>");
        foreach (var item in Items(props["items"]))
        {
            output.Append("<div class='card feature'>");
            var icon = Text(item["icon"]);
            if (!string.IsNullOrEmpty(icon))
            {
                output.Append("<div class='ficon'>")
                    .Append(MarkdownEngine.EscapeHtml(icon))
                    .Append("</div>");
            }

            output.Append("<h3>")
                .Append(MarkdownEngine.InlineMarkdown(Text(item["title"]) ?? string.Empty))
                .Append("</h3>");

            var text = Text(item["text"]);
            if (!string.IsNullOrEmpty(text))
            {
                output.Append("<p>")
                    .Append(MarkdownEngine.InlineMarkdown(text))
                    .Append("</p>");
            }
            output.Append("</div>");
        }
        output.Append("</div>");
        return output.ToString();
    }

    private static string RenderCards(JsonObject props)
    {
        var output = new StringBuilder(Head(props));
        output.Append("<div class='grid cards'>");
        foreach (var item in Items(props["items"]))
        {
            var url = Safe(Text(item["url"]));
            var tag = url.Length > 0 ? "a" : "div";
            output.Append('<').Append(tag).Append(" class='card media-card'");
            if (url.Length > 0)
            {
                output.Append(" href='").Append(MarkdownEngine.EscapeHtml(url)).Append("'");
                if (IsHttp(url))
                    output.Append(" target='_blank' rel='noopener'");
            }
            output.Append('>');

            var image = SafeImage(Text(item["image"]));
            if (image.Length > 0)
            {
                output.Append("<div class='card-img'><img src='")
                    .Append(MarkdownEngine.EscapeHtml(image))
                    .Append("' alt='")
                    .Append(MarkdownEngine.EscapeHtml(Text(item["title"]) ?? string.Empty))
                    .Append("'></div>");
            }

            output.Append("<div class='card-body'>");
            var label = Text(item["tag"]);
            if (!string.IsNullOrEmpty(label))
            {
                output.Append("<span class='pill'>")
                    .Append(MarkdownEngine.EscapeHtml(label))
                    .Append("</span>");
            }

            output.Append("<h3>")
                .Append(MarkdownEngine.InlineMarkdown(Text(item["title"]) ?? string.Empty))
                .Append("</h3>");

            var text = Text(item["text"]);
            if (!string.IsNullOrEmpty(text))
            {
                output.Append("<p>")
                    .Append(MarkdownEngine.InlineMarkdown(text))
                    .Append("</p>");
            }

            output.Append("</div></").Append(tag).Append('>');
        }
        output.Append("</div>");
        return output.ToString();
    }

    private static string RenderGallery(JsonObject props)
    {
        var output = new StringBuilder(Head(props));
        var rendered = 0;
        output.Append("<div class='gallery'>");

        foreach (var item in Items(props["items"]))
        {
            var image = SafeImage(Text(item["image"]));
            if (image.Length == 0)
                continue;

            rendered++;
            var caption = Text(item["caption"]) ?? string.Empty;
            output.Append("<figure class='shot'><a href='")
                .Append(MarkdownEngine.EscapeHtml(image))
                .Append("' target='_blank' rel='noopener'><img src='")
                .Append(MarkdownEngine.EscapeHtml(image))
                .Append("' alt='")
                .Append(MarkdownEngine.EscapeHtml(caption))
                .Append("'></a>");
            if (caption.Length > 0)
            {
                output.Append("<figcaption>")
                    .Append(MarkdownEngine.EscapeHtml(caption))
                    .Append("</figcaption>");
            }
            output.Append("</figure>");
        }

        output.Append("</div>");
        return rendered == 0
            ? Head(props) + "<div class='missing'>Adicione imagens à galeria.</div>"
            : output.ToString();
    }

    private static string RenderVideo(JsonObject props)
    {
        var url = Safe(Text(props["url"]));
        if (url.Length == 0)
            return "<div class='missing'>Informe o link do vídeo.</div>";

        var caption = Text(props["caption"]) ?? string.Empty;
        var youtube = YoutubePattern().Match(url);
        var vimeo = VimeoPattern().Match(url);
        string media;

        if (youtube.Success)
        {
            var src = "https://www.youtube-nocookie.com/embed/" + youtube.Groups[1].Value;
            media = "<iframe src='" + MarkdownEngine.EscapeHtml(src) +
                    "' title='" + MarkdownEngine.EscapeHtml(caption.Length > 0 ? caption : "Vídeo") +
                    "' loading='lazy' allow='accelerometer; encrypted-media; picture-in-picture; fullscreen' allowfullscreen></iframe>";
        }
        else if (vimeo.Success)
        {
            var src = "https://player.vimeo.com/video/" + vimeo.Groups[1].Value;
            media = "<iframe src='" + MarkdownEngine.EscapeHtml(src) +
                    "' title='" + MarkdownEngine.EscapeHtml(caption.Length > 0 ? caption : "Vídeo") +
                    "' loading='lazy' allow='picture-in-picture; fullscreen' allowfullscreen></iframe>";
        }
        else
        {
            media = "<video src='" + MarkdownEngine.EscapeHtml(url) +
                    "' controls preload='metadata'></video>";
        }

        return "<figure class='figure s-wide rounded'><div class='ratio'>" +
               media + "</div>" +
               (caption.Length > 0
                   ? "<figcaption>" + MarkdownEngine.EscapeHtml(caption) + "</figcaption>"
                   : string.Empty) +
               "</figure>";
    }

    private static string RenderTestimonials(JsonObject props)
    {
        var output = new StringBuilder(Head(props));
        output.Append("<div class='grid testimonials'>");
        foreach (var item in Items(props["items"]))
        {
            var author = Text(item["author"]) ?? string.Empty;
            output.Append("<figure class='card testimonial'><blockquote>“")
                .Append(MarkdownEngine.InlineMarkdown(Text(item["text"]) ?? string.Empty))
                .Append("”</blockquote><figcaption>");

            var image = SafeImage(Text(item["image"]));
            if (image.Length > 0)
            {
                output.Append("<img class='avatar' src='")
                    .Append(MarkdownEngine.EscapeHtml(image))
                    .Append("' alt='")
                    .Append(MarkdownEngine.EscapeHtml(author))
                    .Append("'>");
            }
            else
            {
                output.Append("<span class='avatar ph'>")
                    .Append(MarkdownEngine.EscapeHtml(
                        author.Length > 0 ? author[..1] : "?"))
                    .Append("</span>");
            }

            output.Append("<span><strong>")
                .Append(MarkdownEngine.EscapeHtml(author))
                .Append("</strong>");

            var role = Text(item["role"]);
            if (!string.IsNullOrEmpty(role))
            {
                output.Append("<small>")
                    .Append(MarkdownEngine.EscapeHtml(role))
                    .Append("</small>");
            }

            output.Append("</span></figcaption></figure>");
        }
        output.Append("</div>");
        return output.ToString();
    }

    private static string RenderStats(JsonObject props)
    {
        var output = new StringBuilder(Head(props));
        output.Append("<div class='stats'>");
        foreach (var item in Items(props["items"]))
        {
            output.Append("<div class='stat'><strong>")
                .Append(MarkdownEngine.EscapeHtml(Text(item["value"]) ?? string.Empty))
                .Append("</strong><span>")
                .Append(MarkdownEngine.EscapeHtml(Text(item["label"]) ?? string.Empty))
                .Append("</span></div>");
        }
        output.Append("</div>");
        return output.ToString();
    }

    private static string RenderTimeline(JsonObject props)
    {
        var output = new StringBuilder(Head(props));
        output.Append("<ol class='timeline'>");
        foreach (var item in Items(props["items"]))
        {
            output.Append("<li><span class='when'>")
                .Append(MarkdownEngine.EscapeHtml(Text(item["date"]) ?? string.Empty))
                .Append("</span><div><h3>")
                .Append(MarkdownEngine.InlineMarkdown(Text(item["title"]) ?? string.Empty))
                .Append("</h3>");

            var text = Text(item["text"]);
            if (!string.IsNullOrEmpty(text))
                output.Append(MarkdownEngine.Render(text));

            output.Append("</div></li>");
        }
        output.Append("</ol>");
        return output.ToString();
    }

    private static string RenderFaq(JsonObject props)
    {
        var output = new StringBuilder(Head(props));
        output.Append("<div class='faq'>");
        var openFirst = Bool(props["openFirst"], true);
        var index = 0;
        foreach (var item in Items(props["items"]))
        {
            output.Append("<details");
            if (openFirst && index == 0)
                output.Append(" open");
            output.Append("><summary>")
                .Append(MarkdownEngine.InlineMarkdown(Text(item["q"]) ?? string.Empty))
                .Append("</summary><div class='prose'>")
                .Append(MarkdownEngine.Render(Text(item["a"]) ?? string.Empty))
                .Append("</div></details>");
            index++;
        }
        output.Append("</div>");
        return output.ToString();
    }

    private static string RenderCta(JsonObject props)
    {
        return "<div class='cta-box'><div><h2>" +
               MarkdownEngine.InlineMarkdown(Text(props["title"]) ?? string.Empty) +
               "</h2>" +
               (!string.IsNullOrEmpty(Text(props["text"]))
                   ? "<p>" + MarkdownEngine.InlineMarkdown(Text(props["text"])!) + "</p>"
                   : string.Empty) +
               "</div>" + Buttons(props["buttons"]) + "</div>";
    }

    private static string RenderColumns(JsonObject props)
    {
        var output = new StringBuilder(Head(props));
        output.Append("<div class='grid columns'>");
        foreach (var item in Items(props["items"]))
        {
            output.Append("<div class='prose col'>");
            var title = Text(item["title"]);
            if (!string.IsNullOrEmpty(title))
            {
                output.Append("<h3>")
                    .Append(MarkdownEngine.InlineMarkdown(title))
                    .Append("</h3>");
            }
            output.Append(MarkdownEngine.Render(Text(item["markdown"]) ?? string.Empty))
                .Append("</div>");
        }
        output.Append("</div>");
        return output.ToString();
    }

    private static string RenderPricing(JsonObject props)
    {
        var output = new StringBuilder(Head(props));
        output.Append("<div class='grid pricing'>");
        foreach (var item in Items(props["items"]))
        {
            var highlight = Bool(item["highlight"], false);
            output.Append("<div class='card plan")
                .Append(highlight ? " hot" : string.Empty)
                .Append("'>");

            if (highlight)
                output.Append("<span class='pill'>Mais escolhido</span>");

            output.Append("<h3>")
                .Append(MarkdownEngine.EscapeHtml(Text(item["name"]) ?? string.Empty))
                .Append("</h3><div class='price'><strong>")
                .Append(MarkdownEngine.EscapeHtml(Text(item["price"]) ?? string.Empty))
                .Append("</strong><span>")
                .Append(MarkdownEngine.EscapeHtml(Text(item["period"]) ?? string.Empty))
                .Append("</span></div>");

            var description = Text(item["description"]);
            if (!string.IsNullOrEmpty(description))
            {
                output.Append("<p>")
                    .Append(MarkdownEngine.EscapeHtml(description))
                    .Append("</p>");
            }

            output.Append("<ul class='checks'>");
            foreach (var feature in Lines(Text(item["features"])))
            {
                output.Append("<li>")
                    .Append(MarkdownEngine.EscapeHtml(feature))
                    .Append("</li>");
            }
            output.Append("</ul>");

            var button = new JsonArray
            {
                new JsonObject
                {
                    ["label"] = Text(item["buttonLabel"]) ?? "Escolher",
                    ["url"] = Text(item["url"]) ?? "#",
                    ["variant"] = highlight ? "primary" : "secondary"
                }
            };
            output.Append(Buttons(button)).Append("</div>");
        }
        output.Append("</div>");
        return output.ToString();
    }

    private static string RenderContact(JsonObject props)
    {
        var output = new StringBuilder("<div class='contact'><h2>")
            .Append(MarkdownEngine.InlineMarkdown(Text(props["title"]) ?? string.Empty))
            .Append("</h2>");

        var text = Text(props["text"]);
        if (!string.IsNullOrEmpty(text))
        {
            output.Append("<p class='sub'>")
                .Append(MarkdownEngine.InlineMarkdown(text))
                .Append("</p>");
        }

        var contactButtons = new JsonArray();
        var email = Text(props["email"]);
        if (!string.IsNullOrWhiteSpace(email))
        {
            contactButtons.Add(
                new JsonObject
                {
                    ["label"] = "✉ " + email,
                    ["url"] = "mailto:" + email,
                    ["variant"] = "primary"
                });
        }

        var phone = Text(props["phone"]);
        if (!string.IsNullOrWhiteSpace(phone))
        {
            contactButtons.Add(
                new JsonObject
                {
                    ["label"] = "☏ " + phone,
                    ["url"] = "tel:" + PhonePattern().Replace(phone, string.Empty),
                    ["variant"] = "secondary"
                });
        }

        output.Append(Buttons(contactButtons));

        var links = Items(props["links"]);
        if (links.Count > 0)
        {
            output.Append("<div class='links'>");
            foreach (var link in links)
            {
                var url = Safe(Text(link["url"]));
                if (url.Length == 0)
                    continue;

                var label = Text(link["label"]);
                output.Append("<a href='")
                    .Append(MarkdownEngine.EscapeHtml(url))
                    .Append("' target='_blank' rel='noopener'>")
                    .Append(MarkdownEngine.EscapeHtml(
                        string.IsNullOrEmpty(label) ? url : label))
                    .Append(" ↗</a>");
            }
            output.Append("</div>");
        }

        output.Append("</div>");
        return output.ToString();
    }

    private static string RenderCountdown(JsonObject props)
    {
        var date = (Text(props["date"]) ?? string.Empty).Replace(' ', 'T');
        return "<div class='countdown' data-until='" +
               MarkdownEngine.EscapeHtml(date) +
               "' data-done='" +
               MarkdownEngine.EscapeHtml(Text(props["done"]) ?? string.Empty) +
               "'><h2>" +
               MarkdownEngine.InlineMarkdown(Text(props["title"]) ?? string.Empty) +
               "</h2><div class='cd'>" +
               string.Concat(
                   new[] { "dias", "horas", "min", "seg" }
                       .Select(unit => "<div><strong>--</strong><span>" + unit + "</span></div>")) +
               "</div></div>";
    }

    private static string RenderCode(JsonObject props)
    {
        return Head(props) +
               "<pre class='code' data-lang='" +
               MarkdownEngine.EscapeHtml(Text(props["language"]) ?? string.Empty) +
               "'><button class='copy' type='button'>Copiar</button><code>" +
               MarkdownEngine.EscapeHtml(Text(props["code"]) ?? string.Empty) +
               "</code></pre>";
    }

    private static string RenderDivider(JsonObject props)
    {
        var style = Text(props["style"]) ?? "line";
        return style == "wave"
            ? "<svg class='wave' viewBox='0 0 1200 40' preserveAspectRatio='none' aria-hidden='true'><path d='M0 20 Q150 0 300 20 T600 20 T900 20 T1200 20' fill='none' stroke='currentColor' stroke-width='2'/></svg>"
            : "<div class='divider d-" + CssToken(style) + "' role='separator'></div>";
    }

    private static string RenderAbout(JsonObject props)
    {
        var image = SafeImage(Text(props["image"]));
        return "<div class='sheet bk-about'><h2>" +
               MarkdownEngine.EscapeHtml(Text(props["title"]) ?? "Sobre o autor") +
               "</h2>" +
               (image.Length > 0
                   ? "<figure class='bk-photo'><img src='" +
                     MarkdownEngine.EscapeHtml(image) +
                     "' alt='" +
                     MarkdownEngine.EscapeHtml(Text(props["title"]) ?? "Sobre o autor") +
                     "'></figure>"
                   : string.Empty) +
               "<div class='prose'>" +
               MarkdownEngine.Render(Text(props["markdown"]) ?? string.Empty) +
               "</div></div>";
    }

    private static string RenderCopyright(JsonObject props) =>
        "<div class='sheet bk-full bk-front bk-copy'><div class='prose'>" +
        MarkdownEngine.Render(Text(props["markdown"]) ?? string.Empty) +
        "</div></div>";

    private static string RenderDedication(JsonObject props)
    {
        var kind = CssToken(Text(props["kind"]) ?? "dedication");
        var author = Text(props["author"]);
        return "<div class='sheet bk-full bk-front bk-ded k-" + kind +
               "'><div class='bk-ded-in'><div class='prose'>" +
               MarkdownEngine.Render(Text(props["markdown"]) ?? string.Empty) +
               "</div>" +
               (!string.IsNullOrEmpty(author)
                   ? "<p class='bk-ded-author'>— " +
                     MarkdownEngine.InlineMarkdown(author) + "</p>"
                   : string.Empty) +
               "</div></div>";
    }

    private static string RenderColophon(JsonObject props) =>
        "<div class='sheet bk-full bk-colophon'><div class='prose'>" +
        MarkdownEngine.Render(Text(props["markdown"]) ?? string.Empty) +
        "</div></div>";

    private static string Head(JsonObject props)
    {
        var title = Text(props["title"]) ?? string.Empty;
        var subtitle = Text(props["subtitle"]) ?? string.Empty;
        if (title.Length == 0 && subtitle.Length == 0)
            return string.Empty;

        return "<header class='section-head'>" +
               (title.Length > 0
                   ? "<h2>" + MarkdownEngine.InlineMarkdown(title) + "</h2>"
                   : string.Empty) +
               (subtitle.Length > 0
                   ? "<p>" + MarkdownEngine.InlineMarkdown(subtitle) + "</p>"
                   : string.Empty) +
               "</header>";
    }

    private static string Buttons(JsonNode? node)
    {
        var output = new StringBuilder();
        var count = 0;
        foreach (var item in Items(node))
        {
            var url = Safe(Text(item["url"]));
            var label = Text(item["label"]) ?? string.Empty;
            if (url.Length == 0 || label.Length == 0)
                continue;

            if (count++ == 0)
                output.Append("<div class='buttons'>");

            output.Append("<a class='btn ")
                .Append(CssToken(Text(item["variant"]) ?? "primary"))
                .Append("' href='")
                .Append(MarkdownEngine.EscapeHtml(url))
                .Append("'");

            if (IsHttp(url))
                output.Append(" target='_blank' rel='noopener'");

            output.Append('>')
                .Append(MarkdownEngine.EscapeHtml(label))
                .Append("</a>");
        }

        if (count > 0)
            output.Append("</div>");
        return output.ToString();
    }

    private static IReadOnlyList<JsonObject> Items(JsonNode? node) =>
        Array.AsReadOnly(
            (node as JsonArray ?? [])
                .OfType<JsonObject>()
                .ToArray());

    private static IReadOnlyList<string> Lines(string? value) =>
        Array.AsReadOnly(
            (value ?? string.Empty)
                .Split(
                    '\n',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .ToArray());

    private static string? Text(JsonNode? node) =>
        PageDocument.StringValue(node);

    private static bool Bool(JsonNode? node, bool fallback) =>
        node is null ? fallback : PageDocument.BoolValue(node, fallback);

    private static string Safe(string? value)
    {
        var safe = MarkdownEngine.SafeUrl(value);
        return string.IsNullOrWhiteSpace(safe) || safe == "#" ? string.Empty : safe;
    }

    private static string SafeImage(string? value) => Safe(value);

    private static bool IsHttp(string value) =>
        value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private static string CssToken(string? value)
    {
        var source = value ?? string.Empty;
        var chars = source
            .Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_')
            .ToArray();
        return chars.Length == 0 ? "default" : new string(chars);
    }

    [GeneratedRegex(
        @"(?:youtube\.com\/(?:watch\?v=|embed\/|shorts\/)|youtu\.be\/)([\w-]{6,})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex YoutubePattern();

    [GeneratedRegex(
        @"vimeo\.com\/(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VimeoPattern();

    [GeneratedRegex(@"[^\d+]", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();
}
