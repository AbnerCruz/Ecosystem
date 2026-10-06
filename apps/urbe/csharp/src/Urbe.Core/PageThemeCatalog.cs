using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core;

public sealed record PageThemeFont(
    string Id,
    string Label,
    string Stack,
    string GoogleQuery);

public sealed record PageThemePalette(
    string Background,
    string Surface,
    string Text,
    string Muted,
    string Primary,
    string Accent,
    string Border,
    string OnPrimary);

public sealed record PageThemePreset(
    string Id,
    string Label,
    string DefaultMode,
    string HeadingFont,
    string BodyFont,
    int Radius,
    string BackgroundStyle,
    PageThemePalette Light,
    PageThemePalette Dark);

public sealed record ResolvedPageTheme(
    PageThemePreset Preset,
    string Mode,
    PageThemeFont HeadingFont,
    PageThemeFont BodyFont,
    PageThemePalette Light,
    PageThemePalette Dark,
    double Scale,
    int Width,
    string Spacing,
    string Shadow);

/// <summary>
/// Canonical C# projection of the pure theme domain from pages/engine.js.
/// Unknown persisted keys remain untouched; this type only resolves known
/// values for validation/rendering.
/// </summary>
public static partial class PageThemeCatalog
{
    private static readonly IReadOnlyDictionary<string, PageThemeFont> FontsMap =
        new Dictionary<string, PageThemeFont>(StringComparer.Ordinal)
        {
            ["inter"] = new("inter", "Inter", "'Inter',system-ui,sans-serif", "Inter:wght@400;500;600;700;800"),
            ["manrope"] = new("manrope", "Manrope", "'Manrope',system-ui,sans-serif", "Manrope:wght@400;500;600;700;800"),
            ["grotesk"] = new("grotesk", "Space Grotesk", "'Space Grotesk',system-ui,sans-serif", "Space+Grotesk:wght@400;500;600;700"),
            ["dmsans"] = new("dmsans", "DM Sans", "'DM Sans',system-ui,sans-serif", "DM+Sans:wght@400;500;700"),
            ["outfit"] = new("outfit", "Outfit", "'Outfit',system-ui,sans-serif", "Outfit:wght@400;500;600;700;800"),
            ["playfair"] = new("playfair", "Playfair Display", "'Playfair Display',Georgia,serif", "Playfair+Display:wght@500;600;700;800"),
            ["fraunces"] = new("fraunces", "Fraunces", "'Fraunces',Georgia,serif", "Fraunces:wght@400;600;700;800"),
            ["lora"] = new("lora", "Lora", "'Lora',Georgia,serif", "Lora:wght@400;500;600;700"),
            ["merriweather"] = new("merriweather", "Merriweather", "'Merriweather',Georgia,serif", "Merriweather:wght@400;700"),
            ["garamond"] = new("garamond", "EB Garamond", "'EB Garamond',Garamond,Georgia,serif", "EB+Garamond:ital,wght@0,400;0,500;0,600;0,700;1,400"),
            ["cormorant"] = new("cormorant", "Cormorant Garamond", "'Cormorant Garamond',Garamond,Georgia,serif", "Cormorant+Garamond:ital,wght@0,500;0,600;0,700;1,500"),
            ["crimson"] = new("crimson", "Crimson Pro", "'Crimson Pro',Georgia,serif", "Crimson+Pro:ital,wght@0,400;0,600;0,700;1,400"),
            ["baskerville"] = new("baskerville", "Libre Baskerville", "'Libre Baskerville',Baskerville,Georgia,serif", "Libre+Baskerville:ital,wght@0,400;0,700;1,400"),
            ["mono"] = new("mono", "JetBrains Mono", "'JetBrains Mono',ui-monospace,monospace", "JetBrains+Mono:wght@400;600;700"),
            ["system"] = new("system", "Do sistema", "system-ui,-apple-system,'Segoe UI',Roboto,sans-serif", ""),
            ["serif"] = new("serif", "Serifa do sistema", "Georgia,'Times New Roman',serif", "")
        };

    private static PageThemePalette P(
        string bg, string surface, string text, string muted,
        string primary, string accent, string border, string? ink = null) =>
        new(bg, surface, text, muted, primary, accent, border, ink ?? "#ffffff");

    private static readonly IReadOnlyDictionary<string, PageThemePreset> PresetsMap =
        new Dictionary<string, PageThemePreset>(StringComparer.Ordinal)
        {
            ["aurora"] = new("aurora","Aurora","dark","outfit","inter",18,"mesh",
                P("#f6f7fd","#ffffff","#141a2e","#5b6380","#4f6bff","#a855f7","rgba(20,26,46,.1)"),
                P("#0a0e1a","#121a2e","#e9edf8","#9aa5c4","#7c9cff","#c084fc","rgba(148,163,212,.16)","#0a0e1a")),
            ["papel"] = new("papel","Papel","light","playfair","lora",6,"plain",
                P("#fbf8f3","#ffffff","#1f1b16","#6b6257","#b4532a","#2f6f5e","rgba(31,27,22,.12)"),
                P("#1a1714","#231f1b","#f1ebe2","#b3a898","#e0845a","#6fbfa7","rgba(241,235,226,.12)","#1a1714")),
            ["grafite"] = new("grafite","Grafite","dark","inter","inter",10,"plain",
                P("#fafafa","#ffffff","#111113","#6b6b73","#111113","#52525b","rgba(0,0,0,.1)"),
                P("#0c0c0e","#16161a","#f4f4f5","#8e8e97","#fafafa","#a1a1aa","rgba(255,255,255,.1)","#0c0c0e")),
            ["oceano"] = new("oceano","Oceano","light","manrope","manrope",16,"gradient",
                P("#f2f8fb","#ffffff","#0b2233","#4e6878","#0077b6","#00b4d8","rgba(11,34,51,.1)"),
                P("#07161f","#0d2230","#e3f2f9","#8fb0c2","#38bdf8","#22d3ee","rgba(227,242,249,.12)","#07161f")),
            ["floresta"] = new("floresta","Floresta","dark","fraunces","dmsans",14,"mesh",
                P("#f4f8f5","#ffffff","#10231a","#51685c","#15803d","#ca8a04","rgba(16,35,26,.1)"),
                P("#0b1310","#121e19","#e7f2ec","#94ab9f","#4ade80","#facc15","rgba(231,242,236,.12)","#0b1310")),
            ["entardecer"] = new("entardecer","Entardecer","light","grotesk","dmsans",20,"gradient",
                P("#fff7ef","#ffffff","#2a1409","#7a5a48","#ea580c","#db2777","rgba(42,20,9,.1)"),
                P("#1a0f0a","#261711","#fbe9dd","#c4a594","#fb923c","#f472b6","rgba(251,233,221,.12)","#1a0f0a")),
            ["neon"] = new("neon","Neon","dark","grotesk","inter",12,"dots",
                P("#f7f7ff","#ffffff","#10102a","#5c5f87","#0891b2","#db2777","rgba(16,16,42,.1)"),
                P("#06060b","#0f0f19","#eef0ff","#8d90b3","#22d3ee","#f472b6","rgba(34,211,238,.18)","#06060b")),
            ["livro"] = new("livro","Livro clássico","light","cormorant","garamond",0,"plain",
                P("#f7f2e7","#fffdf7","#221d17","#6f6557","#7a2e1f","#9a7b3f","rgba(34,29,23,.14)"),
                P("#1b1814","#24201a","#efe7d8","#b3a792","#d9825f","#c9a861","rgba(239,231,216,.14)","#1b1814")),
            ["moderno"] = new("moderno","Livro moderno","light","grotesk","crimson",0,"plain",
                P("#f1f1ee","#ffffff","#141414","#6a6a66","#1f4fd6","#e4572e","rgba(20,20,20,.12)"),
                P("#121212","#1c1c1c","#ededea","#9d9d98","#7aa0ff","#ff8a65","rgba(237,237,234,.12)","#121212")),
            ["lavanda"] = new("lavanda","Lavanda","light","manrope","inter",18,"mesh",
                P("#faf7ff","#ffffff","#1c1433","#6a5f86","#7c3aed","#0ea5e9","rgba(28,20,51,.1)"),
                P("#110c1d","#1a132b","#efe9ff","#a89bc7","#a78bfa","#38bdf8","rgba(239,233,255,.12)","#110c1d"))
        };

    public static IReadOnlyDictionary<string, PageThemeFont> Fonts => FontsMap;
    public static IReadOnlyDictionary<string, PageThemePreset> Presets => PresetsMap;

    public static ResolvedPageTheme Resolve(JsonObject? theme)
    {
        theme ??= new JsonObject();
        var presetId = PageDocument.StringValue(theme["preset"]) ?? "aurora";
        if (!PresetsMap.TryGetValue(presetId, out var preset))
            preset = PresetsMap["aurora"];

        var mode = PageDocument.StringValue(theme["mode"]);
        if (mode is not "dark" and not "light" and not "auto")
            mode = preset.DefaultMode;

        var headingId = PageDocument.StringValue(theme["headingFont"]);
        if (string.IsNullOrEmpty(headingId) || !FontsMap.ContainsKey(headingId))
            headingId = preset.HeadingFont;

        var bodyId = PageDocument.StringValue(theme["bodyFont"]);
        if (string.IsNullOrEmpty(bodyId) || !FontsMap.ContainsKey(bodyId))
            bodyId = preset.BodyFont;

        var scale = Number(theme["scale"], 1);
        var width = (int)Math.Round(Number(theme["width"], 1120));
        var spacing = PageDocument.StringValue(theme["spacing"]) ?? "comfortable";
        var shadow = PageDocument.StringValue(theme["shadow"]) ?? "soft";

        var light = OverridePalette(preset.Light, theme);
        var dark = OverridePalette(preset.Dark, theme);

        return new ResolvedPageTheme(
            preset,
            mode,
            FontsMap[headingId],
            FontsMap[bodyId],
            light,
            dark,
            Math.Clamp(scale, .85, 1.3),
            Math.Clamp(width, 640, 1600),
            spacing,
            shadow);
    }

    internal static void Validate(
        JsonObject theme,
        List<PageNormalizationIssue> errors)
    {
        Select(theme, "preset", PresetsMap.Keys, "theme.preset", errors);
        Select(theme, "mode", ["", "dark", "light", "auto"], "theme.mode", errors, allowEmpty: true);
        Select(theme, "headingFont", FontsMap.Keys.Prepend(""), "theme.headingFont", errors, allowEmpty: true);
        Select(theme, "bodyFont", FontsMap.Keys.Prepend(""), "theme.bodyFont", errors, allowEmpty: true);
        Select(theme, "background", ["", "plain", "gradient", "mesh", "dots", "grid"], "theme.background", errors, allowEmpty: true);
        Select(theme, "spacing", ["compact", "comfortable", "airy"], "theme.spacing", errors);
        Select(theme, "shadow", ["none", "soft", "strong"], "theme.shadow", errors);
        Select(theme, "headingWeight", ["", "400", "500", "600", "700", "800", "900"], "theme.headingWeight", errors, allowEmpty: true);
        Select(theme, "headingCase", ["", "upper", "small"], "theme.headingCase", errors, allowEmpty: true);
        Select(theme, "buttonStyle", ["solid", "pill", "square", "outline", "soft"], "theme.buttonStyle", errors);
        Select(theme, "cardStyle", ["elevated", "outlined", "flat", "glass"], "theme.cardStyle", errors);
        Select(theme, "linkStyle", ["underline", "plain", "highlight"], "theme.linkStyle", errors);

        foreach (var key in new[] { "primary", "accent", "bg", "surface", "text", "muted", "border" })
        {
            if (theme[key] is null)
                continue;
            var value = PageDocument.StringValue(theme[key]);
            if (value is null || !ColorPattern().IsMatch(value))
                errors.Add(new PageNormalizationIssue("theme." + key, "Cor inválida."));
        }

        Range(theme, "scale", .85, 1.3, "theme.scale", errors);
        Range(theme, "width", 640, 1600, "theme.width", errors);
        Range(theme, "radius", 0, 32, "theme.radius", errors);
        Range(theme, "headingSpacing", -.08, .3, "theme.headingSpacing", errors);
        Range(theme, "headingScale", .6, 1.6, "theme.headingScale", errors);
        Range(theme, "lineHeight", 1.2, 2.2, "theme.lineHeight", errors);
    }

    internal static void ValidateSectionStyle(
        JsonObject style,
        string path,
        List<PageNormalizationIssue> errors)
    {
        Select(style, "background", ["none", "surface", "primary", "gradient", "image", "inverse"], path + ".background", errors);
        Select(style, "padding", ["none", "s", "m", "l", "xl"], path + ".padding", errors);
        Select(style, "width", ["narrow", "normal", "wide", "full"], path + ".width", errors);
        Select(style, "align", ["left", "center"], path + ".align", errors);
        Select(style, "minHeight", ["none", "half", "screen"], path + ".minHeight", errors);
        Select(style, "animation", ["", "none", "fade", "up", "zoom", "left", "right"], path + ".animation", errors, allowEmpty: true);

        foreach (var key in new[] { "bgColor", "textColor" })
        {
            if (style[key] is null)
                continue;
            var value = PageDocument.StringValue(style[key]);
            if (value is null || !ColorPattern().IsMatch(value))
                errors.Add(new PageNormalizationIssue(path + "." + key, "Cor inválida."));
        }
    }

    public static string GoogleFontsQuery(ResolvedPageTheme theme)
    {
        var values = new[] { theme.HeadingFont, theme.BodyFont }
            .Where(font => font.GoogleQuery.Length > 0)
            .Select(font => font.GoogleQuery)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return values.Length == 0
            ? string.Empty
            : string.Join("&", values.Select(value => "family=" + value));
    }

    public static string BuildCss(JsonObject? theme, JsonObject? layout, bool book)
    {
        var resolved = Resolve(theme);
        var t = theme ?? new JsonObject();
        var background = PageDocument.StringValue(t["background"]);
        if (string.IsNullOrEmpty(background))
            background = resolved.Preset.BackgroundStyle;
        var radius = Number(t["radius"], resolved.Preset.Radius);
        var lineHeight = Number(t["lineHeight"], 1.65);
        var headingScale = Number(t["headingScale"], 1);
        var headingWeight = CssScalar(PageDocument.StringValue(t["headingWeight"]));
        var headingCase = PageDocument.StringValue(t["headingCase"]) ?? "";
        var custom = PageFreeLayout.CssSafe(PageDocument.StringValue(t["css"]));

        var sb = new StringBuilder();
        sb.Append(":root{--bg:").Append(resolved.Light.Background)
          .Append(";--surface:").Append(resolved.Light.Surface)
          .Append(";--text:").Append(resolved.Light.Text)
          .Append(";--muted:").Append(resolved.Light.Muted)
          .Append(";--primary:").Append(resolved.Light.Primary)
          .Append(";--accent:").Append(resolved.Light.Accent)
          .Append(";--border:").Append(resolved.Light.Border)
          .Append(";--on-primary:").Append(resolved.Light.OnPrimary)
          .Append(";--fh:").Append(resolved.HeadingFont.Stack)
          .Append(";--fb:").Append(resolved.BodyFont.Stack)
          .Append(";--radius:").Append(radius.ToString(CultureInfo.InvariantCulture)).Append("px")
          .Append(";--max:").Append(resolved.Width.ToString(CultureInfo.InvariantCulture)).Append("px")
          .Append(";--scale:").Append(resolved.Scale.ToString(CultureInfo.InvariantCulture))
          .Append('}');

        sb.Append(":root[data-theme=dark],html.dark-default{--bg:").Append(resolved.Dark.Background)
          .Append(";--surface:").Append(resolved.Dark.Surface)
          .Append(";--text:").Append(resolved.Dark.Text)
          .Append(";--muted:").Append(resolved.Dark.Muted)
          .Append(";--primary:").Append(resolved.Dark.Primary)
          .Append(";--accent:").Append(resolved.Dark.Accent)
          .Append(";--border:").Append(resolved.Dark.Border)
          .Append(";--on-primary:").Append(resolved.Dark.OnPrimary).Append('}');

        if (resolved.Mode == "auto")
        {
            sb.Append("@media(prefers-color-scheme:dark){:root:not([data-theme=light]){--bg:")
              .Append(resolved.Dark.Background).Append(";--surface:").Append(resolved.Dark.Surface)
              .Append(";--text:").Append(resolved.Dark.Text).Append(";--muted:").Append(resolved.Dark.Muted)
              .Append(";--primary:").Append(resolved.Dark.Primary).Append(";--accent:").Append(resolved.Dark.Accent)
              .Append(";--border:").Append(resolved.Dark.Border).Append(";--on-primary:")
              .Append(resolved.Dark.OnPrimary).Append("}}");
        }

        sb.Append("*{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;background:var(--bg);color:var(--text);font-family:var(--fb);font-size:calc(16px*var(--scale));line-height:")
          .Append(lineHeight.ToString(CultureInfo.InvariantCulture))
          .Append("}main{min-height:70vh}.wrap{width:min(var(--max),calc(100% - 32px));margin:auto}")
          .Append("h1,h2,h3,h4{font-family:var(--fh);line-height:1.12")
          .Append(headingWeight.Length > 0 ? ";font-weight:" + headingWeight : string.Empty)
          .Append(";font-size:calc(1em*").Append(headingScale.ToString(CultureInfo.InvariantCulture)).Append(")}")
          .Append("a{color:var(--primary)}img{max-width:100%;height:auto}.nav{border-bottom:1px solid var(--border);background:var(--surface)}")
          .Append(".nav .wrap{min-height:64px;display:flex;align-items:center;justify-content:space-between;gap:18px}.nav.sticky{position:sticky;top:0;z-index:20}.nav-links{display:flex;gap:14px;align-items:center}.brand{font-family:var(--fh);font-weight:700;text-decoration:none;color:var(--text)}")
          .Append(".btn{display:inline-flex;padding:.72em 1em;border-radius:var(--radius);background:var(--primary);color:var(--on-primary);text-decoration:none}.foot{padding:28px 0;border-top:1px solid var(--border);color:var(--muted)}")
          .Append(".progress{position:fixed;left:0;top:0;height:3px;background:var(--primary);z-index:50}.fab{position:fixed;right:18px;width:46px;height:46px;border:1px solid var(--border);border-radius:50%;background:var(--surface);color:var(--text)}")
          .Append(".fab-theme{bottom:72px}.fab-top{bottom:18px}.prose{max-width:72ch}.grid{display:grid;gap:18px}.g2{grid-template-columns:repeat(2,minmax(0,1fr))}.g3{grid-template-columns:repeat(3,minmax(0,1fr))}.g4{grid-template-columns:repeat(4,minmax(0,1fr))}")
          .Append(".card{background:var(--surface);border:1px solid var(--border);border-radius:var(--radius);padding:18px}.missing{color:var(--muted)}");

        if (background == "gradient")
            sb.Append("body{background:linear-gradient(135deg,var(--bg),color-mix(in srgb,var(--primary) 12%,var(--bg)))}");
        else if (background == "mesh")
            sb.Append("body{background:radial-gradient(circle at 10% 10%,color-mix(in srgb,var(--primary) 15%,transparent),transparent 35%),radial-gradient(circle at 90% 20%,color-mix(in srgb,var(--accent) 12%,transparent),transparent 35%),var(--bg)}");
        else if (background == "dots")
            sb.Append("body{background-image:radial-gradient(var(--border) 1px,transparent 1px);background-size:22px 22px}");
        else if (background == "grid")
            sb.Append("body{background-image:linear-gradient(var(--border) 1px,transparent 1px),linear-gradient(90deg,var(--border) 1px,transparent 1px);background-size:28px 28px}");

        if (headingCase == "upper")
            sb.Append("h1,h2,h3,h4{text-transform:uppercase}");
        else if (headingCase == "small")
            sb.Append("h1,h2,h3,h4{font-variant:small-caps}");

        if (book)
            sb.Append(BuildBookCss(layout));

        if (custom.Length > 0)
            sb.Append(custom);

        sb.Append("@media(max-width:700px){.g2,.g3,.g4{grid-template-columns:1fr}.nav-links{flex-wrap:wrap;justify-content:flex-end}}");
        return sb.ToString();
    }

    private static string BuildBookCss(JsonObject? layout)
    {
        layout ??= new JsonObject();
        var size = PageDocument.StringValue(layout["pageSize"]) ?? "a5";
        var margins = PageDocument.StringValue(layout["margins"]) ?? "normal";
        var page = size switch
        {
            "6x9" => (W: 152.4, H: 228.6),
            "pocket" => (W: 110.0, H: 180.0),
            "a4" => (W: 210.0, H: 297.0),
            "letter" => (W: 215.9, H: 279.4),
            _ => (W: 148.0, H: 210.0)
        };
        var margin = margins switch
        {
            "narrow" => (T: 12.0, B: 14.0, I: 15.0, O: 11.0),
            "wide" => (T: 22.0, B: 24.0, I: 25.0, O: 19.0),
            _ => (T: 17.0, B: 19.0, I: 20.0, O: 15.0)
        };

        var numbers = PageDocument.BoolValue(layout["pageNumbers"], true);
        var running = PageDocument.StringValue(layout["runningHead"]) ?? string.Empty;
        var quote = running.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal);

        var css = new StringBuilder()
            .Append(".bk-wrap{display:flex;flex-direction:column;align-items:center;gap:22px}.sheet{position:relative;width:min(")
            .Append(page.W.ToString(CultureInfo.InvariantCulture)).Append("mm,100%);aspect-ratio:")
            .Append(page.W.ToString(CultureInfo.InvariantCulture)).Append('/')
            .Append(page.H.ToString(CultureInfo.InvariantCulture))
            .Append(";background:var(--surface);color:var(--text);padding:7%;overflow-wrap:break-word}.bk-full{display:flex;flex-direction:column}.bk-cover{padding:0}.bk-chhead{text-align:center;margin:12% 0 8%}.dropcap .bk-text>p:first-of-type::first-letter{float:left;font-family:var(--fh);font-size:3.4em;line-height:.8;padding:.06em .08em 0 0;color:var(--primary)}")
            .Append("@page{size:")
            .Append(page.W.ToString(CultureInfo.InvariantCulture)).Append("mm ")
            .Append(page.H.ToString(CultureInfo.InvariantCulture)).Append("mm;margin:")
            .Append(margin.T.ToString(CultureInfo.InvariantCulture)).Append("mm ")
            .Append(margin.O.ToString(CultureInfo.InvariantCulture)).Append("mm ")
            .Append(margin.B.ToString(CultureInfo.InvariantCulture)).Append("mm ")
            .Append(margin.I.ToString(CultureInfo.InvariantCulture));

        if (numbers)
            css.Append(";@bottom-center{content:counter(page);font:9pt var(--fb);color:#555}");
        if (running.Length > 0)
            css.Append(";@top-center{content:\"").Append(quote).Append("\";font:italic 8.5pt var(--fb);color:#666}");
        css.Append("}@page :left{margin-left:").Append(margin.O.ToString(CultureInfo.InvariantCulture))
            .Append("mm;margin-right:").Append(margin.I.ToString(CultureInfo.InvariantCulture))
            .Append("mm}@page :right{margin-left:").Append(margin.I.ToString(CultureInfo.InvariantCulture))
            .Append("mm;margin-right:").Append(margin.O.ToString(CultureInfo.InvariantCulture))
            .Append("mm}@page cover{margin:0;@bottom-center{content:none}@top-center{content:none}}@page front{@bottom-center{content:none}@top-center{content:none}}@page chapter:first{@top-center{content:none}}")
            .Append("@media print{html,html.book body{background:#fff!important}.book main{display:block;padding:0}.sheet{width:auto;aspect-ratio:auto;box-shadow:none;background:none;break-before:page}.sheet.flow{break-before:auto}.bk-cover{page:cover}.bk-front{page:front}.bk-chap{page:chapter}.fab{display:none}}");
        return css.ToString();
    }

    private static PageThemePalette OverridePalette(PageThemePalette source, JsonObject theme) =>
        source with
        {
            Background = Color(theme, "bg", source.Background),
            Surface = Color(theme, "surface", source.Surface),
            Text = Color(theme, "text", source.Text),
            Muted = Color(theme, "muted", source.Muted),
            Primary = Color(theme, "primary", source.Primary),
            Accent = Color(theme, "accent", source.Accent),
            Border = Color(theme, "border", source.Border)
        };

    private static string Color(JsonObject theme, string key, string fallback)
    {
        var value = PageDocument.StringValue(theme[key]);
        return value is not null && ColorPattern().IsMatch(value) ? value : fallback;
    }

    private static void Select(
        JsonObject obj,
        string key,
        IEnumerable<string> options,
        string path,
        List<PageNormalizationIssue> errors,
        bool allowEmpty = false)
    {
        if (obj[key] is null)
            return;
        var value = PageDocument.StringValue(obj[key]);
        if (value is null || (!allowEmpty && value.Length == 0) ||
            !options.Contains(value, StringComparer.Ordinal))
        {
            errors.Add(new PageNormalizationIssue(path, "Valor inválido."));
        }
    }

    private static void Range(
        JsonObject obj,
        string key,
        double min,
        double max,
        string path,
        List<PageNormalizationIssue> errors)
    {
        if (obj[key] is null)
            return;
        if (!TryNumber(obj[key], out var value) || value < min || value > max)
            errors.Add(new PageNormalizationIssue(path, "Número fora do intervalo permitido."));
    }

    private static double Number(JsonNode? node, double fallback) =>
        TryNumber(node, out var value) ? value : fallback;

    private static bool TryNumber(JsonNode? node, out double value)
    {
        value = 0;
        if (node is not JsonValue scalar)
            return false;
        if (scalar.TryGetValue<double>(out value))
            return true;
        if (scalar.TryGetValue<int>(out var integer))
        {
            value = integer;
            return true;
        }
        return false;
    }

    private static string CssScalar(string? value) =>
        Regex.IsMatch(value ?? string.Empty, @"^[a-zA-Z0-9 .,'""-]{0,80}$", RegexOptions.CultureInvariant)
            ? value ?? string.Empty
            : string.Empty;

    [GeneratedRegex(
        @"^(#[0-9a-f]{3,8}|(rgb|hsl)a?\([\d\s.,%/+-]+\)|transparent|currentColor|[a-z]{3,20})$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ColorPattern();
}
