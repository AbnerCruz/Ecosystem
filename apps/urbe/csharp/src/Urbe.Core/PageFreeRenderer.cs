using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core;

internal sealed record FreeRenderResult(string Html, string Css);

internal static partial class PageFreeRenderer
{
    private const string BaseCss =
        ":where(.fx){box-sizing:border-box;min-width:0}" +
        ":where(.fx-box){display:flex;flex-direction:column;gap:16px}" +
        ":where(.fx-box>.fx){max-width:100%}" +
        ":where(.btn.fx){width:fit-content}" +
        ":where(.sheet>.fx-free){height:100%}" +
        ":where(.sheet>.fx-free>.fx-box){min-height:100%}" +
        ".fx-text>:first-child{margin-top:0}.fx-text>:last-child{margin-bottom:0}" +
        ".fx-text p{margin:0 0 .8em}" +
        ".fx h1,.fx h2,.fx h3,.fx h4,.fx h5,.fx h6,h1.fx,h2.fx,h3.fx,h4.fx,h5.fx,h6.fx{margin:0}" +
        ".fx-img{margin:0;display:block}.fx-img img{width:100%;height:100%;object-fit:var(--fx-fit,cover);border-radius:inherit;display:block}" +
        ".fx-img figcaption{font-size:.85em;color:var(--muted);margin-top:.5em}" +
        ".fx-list{margin:0;padding-left:1.3em}" +
        ".fx-quote{margin:0;padding:0 0 0 1em;border-left:3px solid var(--primary);font-style:italic}" +
        ".fx-quote cite{display:block;font-style:normal;font-size:.85em;color:var(--muted);margin-top:.4em}" +
        ".fx-divider{border:0;border-top:1px solid var(--border);margin:0;width:100%}" +
        ".fx-spacer{height:32px}.fx-icon{font-size:2rem;line-height:1}" +
        "a.fx-box{color:inherit;text-decoration:none}" +
        ".fx-video{position:relative;width:100%;aspect-ratio:16/9}" +
        ".fx-video iframe,.fx-video video{position:absolute;inset:0;width:100%;height:100%;border:0;border-radius:inherit}" +
        ".fx-badge{display:inline-flex;width:fit-content;align-items:center;padding:.2em .75em;border-radius:999px;font-size:.8rem;font-weight:700;letter-spacing:.02em;background:color-mix(in srgb,var(--primary) 16%,transparent);color:var(--primary)}" +
        ".fx-embed{position:relative;width:100%;aspect-ratio:16/10}" +
        ".fx-embed iframe{position:absolute;inset:0;width:100%;height:100%;border:0;border-radius:inherit}" +
        ".fx-table{overflow-x:auto}.fx-formula{overflow-x:auto;text-align:center}" +
        ".fx-break{height:0}@media print{.fx-break{break-before:page;page-break-before:always}}";

    private static readonly IReadOnlyDictionary<string, string> FontStacks =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["inter"] = "'Inter',system-ui,sans-serif",
            ["manrope"] = "'Manrope',system-ui,sans-serif",
            ["grotesk"] = "'Space Grotesk',system-ui,sans-serif",
            ["dmsans"] = "'DM Sans',system-ui,sans-serif",
            ["outfit"] = "'Outfit',system-ui,sans-serif",
            ["playfair"] = "'Playfair Display',Georgia,serif",
            ["fraunces"] = "'Fraunces',Georgia,serif",
            ["lora"] = "'Lora',Georgia,serif",
            ["merriweather"] = "'Merriweather',Georgia,serif",
            ["garamond"] = "'EB Garamond',Garamond,Georgia,serif",
            ["cormorant"] = "'Cormorant Garamond',Garamond,Georgia,serif",
            ["crimson"] = "'Crimson Pro',Georgia,serif",
            ["baskerville"] = "'Libre Baskerville',Baskerville,Georgia,serif",
            ["mono"] = "'JetBrains Mono',ui-monospace,monospace",
            ["system"] = "system-ui,-apple-system,'Segoe UI',Roboto,sans-serif",
            ["serif"] = "Georgia,'Times New Roman',serif"
        };

    public static FreeRenderResult Render(
        JsonObject props,
        DocumentStore? documents,
        string sectionId,
        bool bookFormat)
    {
        var root = props["root"] as JsonObject;
        if (root is null)
        {
            var errors = new List<PageNormalizationIssue>();
            var warnings = new List<PageNormalizationIssue>();
            root = PageFreeLayout.NormalizeRoot(
                null,
                "props.root",
                errors,
                warnings);
        }

        var prefix = CleanToken(sectionId);
        if (prefix.Length == 0)
            prefix = "x";
        prefix += "-";

        var css = new FreeCssBuckets();
        var html = RenderNode(
            root,
            documents,
            prefix,
            css);

        var style = BaseCss + css.Build();
        var body = "<style>" + style + "</style><div class='fx-free'>" +
                   html + "</div>";

        if (!bookFormat)
            return new FreeRenderResult(body, style);

        var sheet = PageDocument.StringValue(props["sheet"]) ?? "page";
        var classes = sheet switch
        {
            "flow" => "sheet flow",
            "full" => "sheet bk-free bk-full bk-bleed",
            _ => "sheet bk-free"
        };

        return new FreeRenderResult(
            "<style>" + style + "</style><div class='" + classes + "'>" +
            "<div class='fx-free'>" + html + "</div></div>",
            style);
    }

    private static string RenderNode(
        JsonObject node,
        DocumentStore? documents,
        string prefix,
        FreeCssBuckets css)
    {
        var type = PageDocument.StringValue(node["type"]) ?? string.Empty;
        var id = PageDocument.StringValue(node["id"]) ?? "n";
        var content = node["content"] as JsonObject ?? new JsonObject();
        var style = node["style"] as JsonObject;
        var tablet = node["tablet"] as JsonObject;
        var mobile = node["mobile"] as JsonObject;

        var className = "fx fx-" + prefix + CleanToken(id);
        var customClass =
            PageDocument.StringValue(style?["className"]);
        if (!string.IsNullOrWhiteSpace(customClass))
            className += " " + CleanClasses(customClass);

        AddCss(
            "." + CssSelector(className.Split(' ')[1]),
            type == "box",
            style,
            tablet,
            mobile,
            css);

        var classAttribute = " class='" +
                             MarkdownEngine.EscapeHtml(className) + "'";

        return type switch
        {
            "box" => RenderBox(
                node,
                content,
                documents,
                prefix,
                css,
                className),
            "heading" => RenderHeading(content, classAttribute),
            "text" => "<div class='" +
                      MarkdownEngine.EscapeHtml(className) +
                      " fx-text'>" +
                      MarkdownEngine.Render(Text(content["text"])) +
                      "</div>",
            "image" => RenderImage(content, className),
            "button" => RenderButton(content, className),
            "list" => RenderList(content, className),
            "quote" => RenderQuote(content, className),
            "icon" => "<span class='" +
                      MarkdownEngine.EscapeHtml(className) +
                      " fx-icon'>" +
                      MarkdownEngine.EscapeHtml(Text(content["emoji"])) +
                      "</span>",
            "divider" => "<hr class='" +
                         MarkdownEngine.EscapeHtml(className) +
                         " fx-divider'>",
            "spacer" => "<div class='" +
                        MarkdownEngine.EscapeHtml(className) +
                        " fx-spacer' aria-hidden='true'></div>",
            "video" => RenderVideo(content, className),
            "note" => RenderNote(content, documents, className),
            "html" => "<div class='" +
                      MarkdownEngine.EscapeHtml(className) +
                      "'>" + Text(content["code"]) + "</div>",
            "table" => "<div class='" +
                       MarkdownEngine.EscapeHtml(className) +
                       " fx-table'>" +
                       MarkdownEngine.Render(Text(content["md"])) +
                       "</div>",
            "code" => "<pre class='" +
                      MarkdownEngine.EscapeHtml(className) +
                      " code'" +
                      (!string.IsNullOrEmpty(Text(content["lang"]))
                          ? " data-lang='" +
                            MarkdownEngine.EscapeHtml(Text(content["lang"])) +
                            "'"
                          : string.Empty) +
                      "><button type='button' class='copy'>Copiar</button><code>" +
                      MarkdownEngine.EscapeHtml(Text(content["code"])) +
                      "</code></pre>",
            "formula" => "<div class='" +
                         MarkdownEngine.EscapeHtml(className) +
                         " fx-formula'>" +
                         MarkdownEngine.Render(
                             "$$" +
                             Text(content["tex"]).Replace(
                                 "$$",
                                 string.Empty,
                                 StringComparison.Ordinal) +
                             "$$") +
                         "</div>",
            "badge" => "<span class='" +
                       MarkdownEngine.EscapeHtml(className) +
                       " fx-badge'>" +
                       MarkdownEngine.EscapeHtml(Text(content["text"])) +
                       "</span>",
            "embed" => RenderEmbed(content, className),
            "pagebreak" => "<div class='" +
                           MarkdownEngine.EscapeHtml(className) +
                           " fx-break' aria-hidden='true'></div>",
            _ => string.Empty
        };
    }

    private static string RenderBox(
        JsonObject node,
        JsonObject content,
        DocumentStore? documents,
        string prefix,
        FreeCssBuckets css,
        string className)
    {
        var tag = Text(content["tag"]);
        if (!new[] { "div", "section", "header", "footer", "article", "aside", "nav" }
                .Contains(tag, StringComparer.Ordinal))
        {
            tag = "div";
        }

        var inner = string.Concat(
            (node["children"] as JsonArray ?? [])
                .OfType<JsonObject>()
                .Select(child =>
                    RenderNode(
                        child,
                        documents,
                        prefix,
                        css)));

        var link = Safe(Text(content["link"]));
        if (link.Length > 0)
        {
            return "<a href='" +
                   MarkdownEngine.EscapeHtml(link) +
                   "' class='" +
                   MarkdownEngine.EscapeHtml(className) +
                   " fx-box'" +
                   (IsHttp(link)
                       ? " target='_blank' rel='noopener'"
                       : string.Empty) +
                   ">" + inner + "</a>";
        }

        return "<" + tag + " class='" +
               MarkdownEngine.EscapeHtml(className) +
               " fx-box'>" +
               inner +
               "</" + tag + ">";
    }

    private static string RenderHeading(
        JsonObject content,
        string classAttribute)
    {
        var level = Math.Clamp(
            PageDocument.IntValue(content["level"]) ?? 2,
            1,
            6);
        return "<h" +
               level.ToString(CultureInfo.InvariantCulture) +
               classAttribute +
               ">" +
               MarkdownEngine.InlineMarkdown(Text(content["text"])) +
               "</h" +
               level.ToString(CultureInfo.InvariantCulture) +
               ">";
    }

    private static string RenderImage(
        JsonObject content,
        string className)
    {
        var source = Safe(Text(content["src"]));
        var alt = Text(content["alt"]);
        var image = source.Length > 0
            ? "<img src='" +
              MarkdownEngine.EscapeHtml(source) +
              "' alt='" +
              MarkdownEngine.EscapeHtml(alt) +
              "' loading='lazy'>"
            : "<div class='missing'>Escolha uma imagem</div>";

        var link = Safe(Text(content["link"]));
        if (link.Length > 0)
        {
            image = "<a href='" +
                    MarkdownEngine.EscapeHtml(link) +
                    "'" +
                    (IsHttp(link)
                        ? " target='_blank' rel='noopener'"
                        : string.Empty) +
                    ">" + image + "</a>";
        }

        var caption = Text(content["caption"]);
        return "<figure class='" +
               MarkdownEngine.EscapeHtml(className) +
               " fx-img'>" +
               image +
               (caption.Length > 0
                   ? "<figcaption>" +
                     MarkdownEngine.InlineMarkdown(caption) +
                     "</figcaption>"
                   : string.Empty) +
               "</figure>";
    }

    private static string RenderButton(
        JsonObject content,
        string className)
    {
        var url = Safe(Text(content["url"]));
        if (url.Length == 0)
            url = "#";
        var variant = CleanToken(Text(content["variant"]));
        if (variant.Length == 0)
            variant = "primary";

        return "<a class='" +
               MarkdownEngine.EscapeHtml(className) +
               " btn btn-" +
               MarkdownEngine.EscapeHtml(variant) +
               "' href='" +
               MarkdownEngine.EscapeHtml(url) +
               "'" +
               (IsHttp(url)
                   ? " target='_blank' rel='noopener'"
                   : string.Empty) +
               ">" +
               MarkdownEngine.EscapeHtml(Text(content["label"])) +
               "</a>";
    }

    private static string RenderList(
        JsonObject content,
        string className)
    {
        var ordered = PageDocument.BoolValue(content["ordered"]);
        var tag = ordered ? "ol" : "ul";
        var items = Text(content["items"])
            .Split(
                '\n',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        return "<" + tag + " class='" +
               MarkdownEngine.EscapeHtml(className) +
               " fx-list'>" +
               string.Concat(
                   items.Select(item =>
                       "<li>" +
                       MarkdownEngine.InlineMarkdown(
                           ListMarkerPattern().Replace(item, string.Empty)) +
                       "</li>")) +
               "</" + tag + ">";
    }

    private static string RenderQuote(
        JsonObject content,
        string className)
    {
        var author = Text(content["author"]);
        return "<blockquote class='" +
               MarkdownEngine.EscapeHtml(className) +
               " fx-quote'>" +
               MarkdownEngine.Render(Text(content["text"])) +
               (author.Length > 0
                   ? "<cite>— " +
                     MarkdownEngine.InlineMarkdown(author) +
                     "</cite>"
                   : string.Empty) +
               "</blockquote>";
    }

    private static string RenderVideo(
        JsonObject content,
        string className)
    {
        var url = Safe(Text(content["url"]));
        if (url.Length == 0)
        {
            return "<div class='" +
                   MarkdownEngine.EscapeHtml(className) +
                   " missing'>Informe o link do vídeo</div>";
        }

        var youtube = YoutubePattern().Match(url);
        var vimeo = VimeoPattern().Match(url);
        string media;
        if (youtube.Success)
        {
            media =
                "<iframe src='https://www.youtube-nocookie.com/embed/" +
                MarkdownEngine.EscapeHtml(youtube.Groups[1].Value) +
                "' title='Vídeo' loading='lazy' allowfullscreen></iframe>";
        }
        else if (vimeo.Success)
        {
            media =
                "<iframe src='https://player.vimeo.com/video/" +
                MarkdownEngine.EscapeHtml(vimeo.Groups[1].Value) +
                "' title='Vídeo' loading='lazy' allowfullscreen></iframe>";
        }
        else
        {
            media = "<video src='" +
                    MarkdownEngine.EscapeHtml(url) +
                    "' controls preload='metadata'></video>";
        }

        return "<div class='" +
               MarkdownEngine.EscapeHtml(className) +
               " fx-video'>" +
               media +
               "</div>";
    }

    private static string RenderNote(
        JsonObject content,
        DocumentStore? documents,
        string className)
    {
        var path = Text(content["path"]);
        var document = PageRenderPlan.FindDocument(documents, path);
        if (document is null)
        {
            return "<div class='" +
                   MarkdownEngine.EscapeHtml(className) +
                   " missing'>Nota não encontrada: " +
                   MarkdownEngine.EscapeHtml(
                       path.Length > 0 ? path : "(escolha uma nota)") +
                   "</div>";
        }

        var showTitle =
            PageDocument.BoolValue(content["showTitle"]);
        var markdown = StripLeadingTitle(
            document.Content,
            document.Title);

        return "<div class='" +
               MarkdownEngine.EscapeHtml(className) +
               " fx-text'>" +
               (showTitle
                   ? "<h2>" +
                     MarkdownEngine.EscapeHtml(document.Title) +
                     "</h2>"
                   : string.Empty) +
               MarkdownEngine.Render(markdown) +
               "</div>";
    }

    private static string RenderEmbed(
        JsonObject content,
        string className)
    {
        var url = Safe(Text(content["url"]));
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return "<div class='" +
                   MarkdownEngine.EscapeHtml(className) +
                   " missing'>Cole um endereço https:// para incorporar</div>";
        }

        return "<div class='" +
               MarkdownEngine.EscapeHtml(className) +
               " fx-embed'><iframe src='" +
               MarkdownEngine.EscapeHtml(url) +
               "' title='" +
               MarkdownEngine.EscapeHtml(Text(content["title"])) +
               "' loading='lazy' referrerpolicy='no-referrer' " +
               "sandbox='allow-scripts allow-same-origin allow-popups allow-forms' " +
               "allowfullscreen></iframe></div>";
    }

    private static void AddCss(
        string selector,
        bool isBox,
        JsonObject? style,
        JsonObject? tablet,
        JsonObject? mobile,
        FreeCssBuckets output)
    {
        AddCssAt(selector, isBox, style, output.Base);
        AddCssAt(selector, isBox, tablet, output.Tablet);
        AddCssAt(selector, isBox, mobile, output.Mobile);
    }

    private static void AddCssAt(
        string selector,
        bool isBox,
        JsonObject? style,
        List<string> output)
    {
        if (style is null)
            return;

        var declaration = Declaration(style, isBox);
        if (declaration.Length > 0)
            output.Add(selector + "{" + declaration + "}");

        var display = Text(style["display"]);
        if (isBox && display == "row")
        {
            output.Add(
                selector +
                ">*{flex:1 1 var(--fx-basis,220px)}");
        }
        else if (isBox &&
                 display is "stack" or "grid")
        {
            output.Add(
                selector +
                ">*{flex:0 1 auto}");
        }

        var custom = PageFreeLayout.CssSafe(Text(style["css"]));
        if (custom.Length == 0)
            return;

        output.Add(
            custom.Contains('{', StringComparison.Ordinal)
                ? custom.Replace("&", selector, StringComparison.Ordinal)
                : selector + "{" + custom + "}");
    }

    private static string Declaration(
        JsonObject style,
        bool isBox)
    {
        var declarations = new List<string>();
        var display = Text(style["display"]);

        if (display == "none")
            declarations.Add("display:none");
        else if (isBox && display == "row")
            declarations.Add(
                "display:flex;flex-direction:row;flex-wrap:wrap");
        else if (isBox && display == "grid")
        {
            var columns = Number(style["columns"]);
            var minCol = Text(style["minCol"]);
            declarations.Add(
                "display:grid;grid-template-columns:" +
                (columns is > 0
                    ? "repeat(" +
                      ((int)columns.Value).ToString(
                          CultureInfo.InvariantCulture) +
                      ",minmax(0,1fr))"
                    : "repeat(auto-fit,minmax(min(100%," +
                      (minCol.Length > 0 ? minCol : "220px") +
                      "),1fr))"));
        }
        else if (isBox && display == "stack")
            declarations.Add(
                "display:flex;flex-direction:column;flex-wrap:nowrap");

        Add(style, declarations, "gap", "gap");
        Add(style, declarations, "minCol", "--fx-basis");

        var justify = Text(style["justify"]);
        if (justify.Length > 0)
            declarations.Add("justify-content:" + FlexAlign(justify));

        var align = Text(style["align"]);
        if (align.Length > 0)
            declarations.Add(
                "align-items:" +
                (align == "start" ? "flex-start" :
                 align == "end" ? "flex-end" :
                 align));

        var self = Text(style["self"]);
        if (self.Length > 0)
            declarations.Add(
                "align-self:" +
                (self == "start" ? "flex-start" :
                 self == "end" ? "flex-end" :
                 self));

        var order = Number(style["order"]);
        if (order is not null)
            declarations.Add(
                "order:" +
                order.Value.ToString(
                    CultureInfo.InvariantCulture));

        var width = Text(style["width"]);
        if (width.Length > 0)
        {
            declarations.Add(
                "width:" +
                (width == "full" ? "100%" : width) +
                ";flex-basis:" +
                (width == "auto" ? "auto" : width) +
                ";flex-grow:0");
        }

        Add(style, declarations, "maxWidth", "max-width");
        Add(style, declarations, "minHeight", "min-height");
        Add(style, declarations, "height", "height");
        Add(style, declarations, "aspect", "aspect-ratio");
        Add(style, declarations, "fit", "--fx-fit");
        Add(style, declarations, "padding", "padding");
        Add(style, declarations, "margin", "margin");

        var bg = Text(style["bg"]);
        var bg2 = Text(style["bg2"]);
        var bgImage = Text(style["bgImage"]);
        var backgrounds = new List<string>();
        if (bg.Length > 0 && bg2.Length > 0)
        {
            backgrounds.Add(
                "linear-gradient(" +
                (Number(style["angle"]) ?? 135)
                    .ToString(CultureInfo.InvariantCulture) +
                "deg," + bg + "," + bg2 + ")");
        }
        if (bgImage.Length > 0)
            backgrounds.Add("url('" + bgImage + "') center/cover no-repeat");
        if (backgrounds.Count > 0)
            declarations.Add("background:" + string.Join(",", backgrounds));
        else if (bg.Length > 0)
            declarations.Add("background:" + bg);

        var color = Text(style["color"]);
        if (color.Length > 0)
        {
            declarations.Add(
                "color:" + color +
                ";--text:" + color +
                ";--muted:color-mix(in srgb," +
                color + " 72%,transparent)");
        }

        var border = Text(style["border"]);
        var borderStyle = Text(style["borderStyle"]);
        var borderColor = Text(style["borderColor"]);
        if (border.Length > 0)
        {
            declarations.Add(
                "border:" + border + " " +
                (borderStyle.Length > 0 ? borderStyle : "solid") + " " +
                (borderColor.Length > 0
                    ? borderColor
                    : "var(--border)"));
        }
        else
        {
            if (borderStyle.Length > 0)
                declarations.Add("border-style:" + borderStyle);
            if (borderColor.Length > 0)
                declarations.Add("border-color:" + borderColor);
        }

        Add(style, declarations, "radius", "border-radius");

        var shadow = Text(style["shadow"]);
        if (shadow.Length > 0)
        {
            declarations.Add(
                "box-shadow:" +
                shadow switch
                {
                    "none" => "none",
                    "strong" => "0 3px 6px rgba(0,0,0,.14),0 24px 50px -18px rgba(0,0,0,.6)",
                    "glow" => "0 0 0 1px color-mix(in srgb,var(--primary) 40%,transparent),0 10px 40px -6px color-mix(in srgb,var(--primary) 55%,transparent)",
                    _ => "0 1px 2px rgba(0,0,0,.08),0 10px 30px -14px rgba(0,0,0,.35)"
                });
        }

        var opacity = Number(style["opacity"]);
        if (opacity is not null)
            declarations.Add("opacity:" + opacity.Value.ToString(CultureInfo.InvariantCulture));

        var font = Text(style["font"]);
        if (font.Length > 0)
        {
            declarations.Add(
                "font-family:" +
                (font == "heading"
                    ? "var(--fh)"
                    : font == "body"
                        ? "var(--fb)"
                        : FontStacks.GetValueOrDefault(font, "var(--fb)")));
        }

        Add(style, declarations, "size", "font-size");
        Add(style, declarations, "weight", "font-weight");

        var lineHeight = Number(style["lineHeight"]);
        if (lineHeight is not null)
            declarations.Add(
                "line-height:" +
                lineHeight.Value.ToString(CultureInfo.InvariantCulture));

        Add(style, declarations, "spacing", "letter-spacing");
        Add(style, declarations, "textAlign", "text-align");
        Add(style, declarations, "transform", "text-transform");

        var italic = Text(style["italic"]);
        if (italic.Length > 0)
            declarations.Add(
                "font-style:" +
                (italic == "yes" ? "italic" : "normal"));

        return string.Join(";", declarations);
    }

    private static void Add(
        JsonObject style,
        List<string> declarations,
        string key,
        string cssKey)
    {
        var value = Text(style[key]);
        if (value.Length > 0)
            declarations.Add(cssKey + ":" + value);
    }

    private static string FlexAlign(string value) =>
        value switch
        {
            "start" => "flex-start",
            "end" => "flex-end",
            "between" => "space-between",
            "around" => "space-around",
            "evenly" => "space-evenly",
            _ => value
        };

    private static string StripLeadingTitle(
        string content,
        string title)
    {
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

    private static string Safe(string? value)
    {
        var safe = MarkdownEngine.SafeUrl(value);
        return string.IsNullOrWhiteSpace(safe) || safe == "#"
            ? string.Empty
            : safe;
    }

    private static bool IsHttp(string value) =>
        value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private static string Text(JsonNode? node) =>
        PageDocument.StringValue(node) ?? string.Empty;

    private static double? Number(JsonNode? node)
    {
        if (node is not JsonValue value)
            return null;
        if (value.TryGetValue<double>(out var number))
            return number;
        if (value.TryGetValue<int>(out var integer))
            return integer;
        return null;
    }

    private static string CleanToken(string value) =>
        TokenPattern().Replace(value, string.Empty);

    private static string CleanClasses(string value) =>
        ClassPattern().Replace(value, string.Empty).Trim();

    private static string CssSelector(string value) =>
        value.Replace(":", "\\:", StringComparison.Ordinal);

    private sealed class FreeCssBuckets
    {
        public List<string> Base { get; } = [];
        public List<string> Tablet { get; } = [];
        public List<string> Mobile { get; } = [];

        public string Build()
        {
            var output = new StringBuilder();
            foreach (var rule in Base)
                output.Append(rule);
            if (Tablet.Count > 0)
            {
                output.Append("@media (max-width:900px){");
                foreach (var rule in Tablet)
                    output.Append(rule);
                output.Append('}');
            }
            if (Mobile.Count > 0)
            {
                output.Append("@media (max-width:600px){");
                foreach (var rule in Mobile)
                    output.Append(rule);
                output.Append('}');
            }
            return output.ToString();
        }
    }

    [GeneratedRegex(@"^\s*([-*+]|\d+[.)])\s+", RegexOptions.CultureInvariant)]
    private static partial Regex ListMarkerPattern();

    [GeneratedRegex(
        @"(?:youtube\.com\/(?:watch\?v=|embed\/|shorts\/)|youtu\.be\/)([\w-]{6,})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex YoutubePattern();

    [GeneratedRegex(
        @"vimeo\.com\/(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VimeoPattern();

    [GeneratedRegex(@"[^\w-]", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();

    [GeneratedRegex(@"[^\w\s-]", RegexOptions.CultureInvariant)]
    private static partial Regex ClassPattern();
}
