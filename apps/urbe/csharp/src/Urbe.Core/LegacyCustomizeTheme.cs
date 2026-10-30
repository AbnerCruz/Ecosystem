using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// src/customize/customize.js (1.8.4-beta), the visual half: the ready-made themes, the fonts,
/// the colour palette of a configuration (paleta) and the screen variables derived from it
/// (variaveis: accent hover/ink/soft, shadows, radii, fonts, sizes, editor width and the
/// data-* attributes). Ported line by line; the native client maps the variables onto its own
/// design tokens because there is no CSS.
/// </summary>
public static class LegacyCustomizeTheme
{
    public static readonly (string Id, string Name, bool Light, (string Key, string Hex)[] Colors)[] PresetData =
    [
        ("escuro", "Escuro", false, [("fundo", "#0e0f11"), ("superficie", "#15161a"), ("superficie2", "#1b1d22"), ("superficie3", "#24262c"), ("linha", "#25272d"), ("linha2", "#33363e"), ("texto", "#ececf0"), ("texto2", "#a3a6af"), ("texto3", "#898c95"), ("destaque", "#8fb3ff"), ("perigo", "#ff7b86"), ("sucesso", "#7fdca6"), ("aviso", "#f1c46e"), ("codigo", "#f0d9a8")]),
        ("meia-noite", "Meia-noite", false, [("fundo", "#0b0d1a"), ("superficie", "#111427"), ("superficie2", "#171a31"), ("superficie3", "#20243f"), ("linha", "#1f2340"), ("linha2", "#2c3156"), ("texto", "#e8e9f7"), ("texto2", "#a2a6c9"), ("texto3", "#868aaf"), ("destaque", "#b39cff"), ("perigo", "#ff7b9c"), ("sucesso", "#7fe0c0"), ("aviso", "#f3c778"), ("codigo", "#ffd59e")]),
        ("floresta", "Floresta", false, [("fundo", "#0d120e"), ("superficie", "#131a14"), ("superficie2", "#19221a"), ("superficie3", "#223024"), ("linha", "#1f2a20"), ("linha2", "#2e3d2f"), ("texto", "#e7efe4"), ("texto2", "#a4b5a0"), ("texto3", "#879883"), ("destaque", "#8fd18a"), ("perigo", "#ff8a7a"), ("sucesso", "#9be08f"), ("aviso", "#e9c66e"), ("codigo", "#e8d9a0")]),
        ("oceano", "Oceano", false, [("fundo", "#081317"), ("superficie", "#0d1b21"), ("superficie2", "#12242b"), ("superficie3", "#193139"), ("linha", "#162a31"), ("linha2", "#23404a"), ("texto", "#e3f1f4"), ("texto2", "#9dbac1"), ("texto3", "#7b9aa2"), ("destaque", "#5fd0e0"), ("perigo", "#ff8490"), ("sucesso", "#78e0b0"), ("aviso", "#f0c674"), ("codigo", "#ffe0a3")]),
        ("vinho", "Vinho", false, [("fundo", "#140c0e"), ("superficie", "#1c1114"), ("superficie2", "#23161a"), ("superficie3", "#301e23"), ("linha", "#2a1a1e"), ("linha2", "#40282e"), ("texto", "#f3e7e9"), ("texto2", "#c0a4aa"), ("texto3", "#9f8288"), ("destaque", "#f08aa0"), ("perigo", "#ff8d7a"), ("sucesso", "#8fdcab"), ("aviso", "#f1c46e"), ("codigo", "#f6d6a8")]),
        ("claro", "Claro", true, [("fundo", "#f6f6f8"), ("superficie", "#ffffff"), ("superficie2", "#f1f2f5"), ("superficie3", "#e7e8ed"), ("linha", "#e3e4e9"), ("linha2", "#d2d4db"), ("texto", "#17181c"), ("texto2", "#555862"), ("texto3", "#656872"), ("destaque", "#3563c7"), ("perigo", "#bf3544"), ("sucesso", "#177846"), ("aviso", "#8f5f07"), ("codigo", "#9a4d12")]),
        ("sepia", "Sépia", true, [("fundo", "#f4ecdc"), ("superficie", "#fbf5e8"), ("superficie2", "#efe5d1"), ("superficie3", "#e6dac2"), ("linha", "#e2d5bb"), ("linha2", "#d3c3a3"), ("texto", "#3b3024"), ("texto2", "#6b5b47"), ("texto3", "#6e5e4a"), ("destaque", "#904f26"), ("perigo", "#af352c"), ("sucesso", "#436a31"), ("aviso", "#81580e"), ("codigo", "#8a3f1c")]),
        ("alto-contraste", "Alto contraste", false, [("fundo", "#000000"), ("superficie", "#0a0a0a"), ("superficie2", "#121212"), ("superficie3", "#1e1e1e"), ("linha", "#5a5a5a"), ("linha2", "#8a8a8a"), ("texto", "#ffffff"), ("texto2", "#f0f0f0"), ("texto3", "#c8c8c8"), ("destaque", "#ffd400"), ("perigo", "#ff5c5c"), ("sucesso", "#4dff88"), ("aviso", "#ffd400"), ("codigo", "#ffe680")]),
    ];

    public static readonly (string Id, string Name, string Css)[] FontData =
    [
        ("sistema", "Do sistema", "-apple-system,BlinkMacSystemFont,\"SF Pro Text\",\"Segoe UI Variable\",\"Segoe UI\",Roboto,\"Inter\",\"Helvetica Neue\",Arial,sans-serif"),
        ("arredondada", "Arredondada", "ui-rounded,\"SF Pro Rounded\",\"Nunito\",\"Varela Round\",\"Quicksand\",\"Segoe UI\",system-ui,sans-serif"),
        ("humanista", "Humanista", "\"Segoe UI\",Candara,Optima,\"Gill Sans\",\"Trebuchet MS\",Ubuntu,sans-serif"),
        ("serifada", "Serifada", "\"Iowan Old Style\",\"Palatino Linotype\",Palatino,\"Book Antiqua\",Georgia,serif"),
        ("leitura", "Leitura", "Charter,\"Bitstream Charter\",\"Sitka Text\",Cambria,Georgia,serif"),
        ("mono", "Monoespaçada", "ui-monospace,\"SF Mono\",SFMono-Regular,\"JetBrains Mono\",Menlo,Consolas,monospace"),
        ("manuscrita", "Manuscrita", "\"Segoe Print\",\"Bradley Hand\",\"Comic Neue\",\"Comic Sans MS\",cursive"),
    ];

    /// <summary>TOKEN: configuration colour name to the CSS variable it feeds.</summary>
    public static readonly (string Key, string Token)[] Tokens =
    [
        ("fundo", "--ui-bg"), ("superficie", "--ui-surface"), ("superficie2", "--ui-surface-2"), ("superficie3", "--ui-surface-3"),
        ("linha", "--ui-line"), ("linha2", "--ui-line-2"), ("texto", "--ui-text"), ("texto2", "--ui-text-2"), ("texto3", "--ui-text-3"),
        ("destaque", "--ui-accent"), ("perigo", "--ui-danger"), ("sucesso", "--ui-ok"), ("aviso", "--ui-warn"), ("codigo", "--ui-code")
    ];

    /// <summary>CANTOS: --ui-r-s, -m, -l, -xl in px.</summary>
    public static readonly (string Id, int[] Radii)[] Corners =
    [
        ("retos", [3, 4, 6, 8]), ("suaves", [8, 12, 16, 22]), ("redondos", [12, 18, 24, 30])
    ];

    /// <summary>LARGURAS: editor column width.</summary>
    public static readonly (string Id, string Width)[] Widths = [("estreita", "600px"), ("media", "720px"), ("larga", "920px"), ("total", "100%")];

    public sealed record Preset(string Id, string Name, bool Light, IReadOnlyDictionary<string, string> Colors);
    public sealed record Palette(bool Light, IReadOnlyDictionary<string, string> Colors);
    /// <summary>A theme file in Personalização/temas (temaSalvo / temasDoVault).</summary>
    public sealed record SavedTheme(string Path, string Name, bool Light, IReadOnlyDictionary<string, string> Colors, bool Valid, string? Base, bool? LightDeclared);
    public sealed record Variables(IReadOnlyList<KeyValuePair<string, string>> Vars, IReadOnlyDictionary<string, string> Attrs, bool Light, string Background);

    public static IReadOnlyList<Preset> Presets { get; } = PresetData.Select(p => new Preset(p.Id, p.Name, p.Light, p.Colors.ToDictionary(c => c.Key, c => c.Hex))).ToArray();

    public static Preset? PresetById(string? id) => Presets.FirstOrDefault(p => p.Id == id);

    /// <summary>fonteCss(v).</summary>
    public static string FontCss(string? value)
    {
        var f = FontData.FirstOrDefault(x => x.Id == value);
        if (f.Id is not null) return f.Css;
        var v = LegacyCustomize.JsTrim(Regex.Replace(value ?? "", @"[;{}<>\\]", ""));
        return v.Length > 0 ? v : FontData[0].Css;
    }

    /// <summary>temaSalvo(nome): the parsed theme object of Personalização/temas, or null.</summary>
    public static SavedTheme? ParseSavedTheme(string path, string title, string content)
    {
        JsonObject? t = null;
        try { t = LegacyCustomize.ParseJs(content) as JsonObject; } catch (System.Text.Json.JsonException) { }
        var colors = new Dictionary<string, string>();
        if (t?["cores"] is JsonObject co)
            foreach (var k in LegacyCustomize.Keys(co))
                if (co[k] is JsonNode n && n is JsonValue jv && jv.TryGetValue<string>(out var s)) colors[k] = s;
        string name = t is not null && t["nome"] is JsonValue nv && nv.TryGetValue<string>(out var ns) && ns.Length > 0 ? ns : Regex.Replace(title, @"\.json$", "", RegexOptions.IgnoreCase);
        bool? declared = t?["claro"] is JsonValue cv && cv.TryGetValue<bool>(out var cb) ? cb : null;
        string? baseId = t?["base"] is JsonValue bv && bv.TryGetValue<string>(out var bs) ? bs : null;
        bool truthy = t?["claro"] is JsonValue tv && (tv.TryGetValue<bool>(out var tb) ? tb : !(tv.TryGetValue<string>(out var ts) && ts.Length == 0) && !(tv.TryGetValue<double>(out var td) && td == 0));
        return new SavedTheme(path, name, truthy, colors, t is not null, baseId, declared);
    }

    // ---------------------------------------------------------------- colours

    public static int[] Rgb(string? h)
    {
        h = LegacyCustomize.Hex(h is null ? null : JsonValue.Create(h)) ?? "#000000";
        return [Convert.ToInt32(h[1..3], 16), Convert.ToInt32(h[3..5], 16), Convert.ToInt32(h[5..7], 16)];
    }

    private static int JsRound(double v) => (int)Math.Floor(v + .5);
    public static string ToHex(IEnumerable<double> a) => "#" + string.Concat(a.Select(v => Math.Max(0, Math.Min(255, JsRound(v))).ToString("x2", CultureInfo.InvariantCulture)));

    public static string Mix(string a, string b, double t)
    {
        var x = Rgb(a); var y = Rgb(b);
        return ToHex(Enumerable.Range(0, 3).Select(i => x[i] + (y[i] - x[i]) * t));
    }

    public static double Luminance(string h)
    {
        var c = Rgb(h).Select(v => { double u = v / 255d; return u <= .03928 ? u / 12.92 : Math.Pow((u + .055) / 1.055, 2.4); }).ToArray();
        return .2126 * c[0] + .7152 * c[1] + .0722 * c[2];
    }

    public static double Contrast(string a, string b)
    {
        double x = Luminance(a), y = Luminance(b);
        return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05);
    }

    private static double Num(JsonNode? v, double min, double max, double def)
    {
        var d = LegacyCustomize.JsToNumber(v);
        return double.IsFinite(d) ? Math.Max(min, Math.Min(max, d)) : def;
    }

    private static JsonObject Sub(JsonObject c, string k) => c[k] as JsonObject ?? new JsonObject();
    private static string Str(JsonObject o, string k, string def) => o[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s : def;

    // ---------------------------------------------------------------- paleta / variaveis

    /// <summary>paleta(c): <paramref name="saved"/> answers temaSalvo(nome) for a theme that is not a preset.</summary>
    public static Palette PaletteOf(JsonObject c, Func<string, SavedTheme?>? saved = null)
    {
        var theme = Str(c, "tema", "escuro");
        var baseP = PresetById(theme);
        SavedTheme? salvo = baseP is null ? saved?.Invoke(theme) : null;
        if (salvo is not null && !salvo.Valid) salvo = null;
        if (baseP is null && salvo is null) baseP = PresetById("escuro");
        bool? light = baseP is not null ? baseP.Light : salvo!.LightDeclared;
        var cores = new Dictionary<string, string>(baseP?.Colors ?? new Dictionary<string, string>());
        if (salvo is not null)
        {
            var reference = salvo.Base is null ? null : PresetById(salvo.Base);
            light ??= reference?.Light ?? false;
            cores = new Dictionary<string, string>((reference ?? PresetById(light.Value ? "claro" : "escuro")!).Colors);
            foreach (var kv in salvo.Colors)
            {
                var h = LegacyCustomize.Hex(JsonValue.Create(kv.Value));
                if (h is not null && Tokens.Any(t => t.Key == kv.Key)) cores[kv.Key] = h;
            }
        }
        var own = Sub(c, "cores");
        foreach (var k in LegacyCustomize.Keys(own))
        {
            var h = LegacyCustomize.Hex(own[k]);
            if (h is not null) cores[k] = h;
        }
        if (own["fundo"] is JsonValue fv && fv.TryGetValue<string>(out var fs) && fs.Length > 0) light = Luminance(cores["fundo"]) > .4;
        return new Palette(light ?? false, cores);
    }

    /// <summary>variaveis(c).</summary>
    public static Variables VariablesOf(JsonObject c, Func<string, SavedTheme?>? saved = null)
    {
        var p = PaletteOf(c, saved);
        var k = p.Colors; bool light = p.Light;
        var v = new List<KeyValuePair<string, string>>();
        void Set(string n, string val) => v.Add(new(n, val));
        foreach (var (key, token) in Tokens) if (k.TryGetValue(key, out var hx) && hx.Length > 0) Set(token, hx);
        var a = k["destaque"];
        var ink = Contrast(a, "#0b1325") >= Contrast(a, "#ffffff") ? "#0b1325" : "#ffffff";
        Set("--ui-accent-hover", light ? Mix(a, "#000000", .12) : Mix(a, "#ffffff", .18));
        Set("--ui-accent-ink", ink);
        Set("--ui-accent-soft", "rgba(" + string.Join(",", Rgb(a)) + "," + (light ? "0.12" : "0.14") + ")");
        Set("--ui-danger-soft", "rgba(" + string.Join(",", Rgb(k["perigo"])) + "," + (light ? "0.1" : "0.12") + ")");
        foreach (var (n, h) in new[] { ("--ui-accent-rgb", a), ("--ui-surface-rgb", k["superficie"]), ("--ui-surface-2-rgb", k["superficie2"]), ("--ui-surface-3-rgb", k["superficie3"]),
                     ("--ui-bg-rgb", k["fundo"]), ("--ui-ok-rgb", k["sucesso"]), ("--ui-danger-rgb", k["perigo"]), ("--ui-warn-rgb", k["aviso"]) })
            Set(n, string.Join(",", Rgb(h)));
        Set("--ui-tint", light ? "0,0,0" : "255,255,255");
        Set("--ui-shadow-1", light ? "0 1px 2px rgba(20,22,30,.08),0 6px 20px rgba(20,22,30,.08)" : "0 1px 2px rgba(0,0,0,.35),0 6px 20px rgba(0,0,0,.28)");
        Set("--ui-shadow-2", light ? "0 18px 60px rgba(20,22,30,.18)" : "0 18px 60px rgba(0,0,0,.55)");
        var texto = Sub(c, "texto"); var forma = Sub(c, "forma"); var editor = Sub(c, "editor");
        var r = Corners.FirstOrDefault(x => x.Id == Str(forma, "cantos", "suaves")).Radii ?? Corners[1].Radii;
        Set("--ui-r-s", r[0] + "px"); Set("--ui-r-m", r[1] + "px"); Set("--ui-r-l", r[2] + "px"); Set("--ui-r-xl", r[3] + "px");
        Set("--ui-font", FontCss(Str(texto, "fonte", "sistema"))); Set("--ui-editor-font", FontCss(Str(texto, "fonteEditor", "sistema")));
        Set("--ui-font-size", LegacyCustomize.JsNumber(Num(texto["tamanho"], 12, 22, 15)) + "px");
        Set("--ui-editor-size", LegacyCustomize.JsNumber(Num(texto["tamanhoEditor"], 13, 28, 17)) + "px");
        Set("--ui-code-size", JsRound(Num(texto["tamanhoEditor"], 13, 28, 17) * .88) + "px");
        Set("--ui-editor-line", LegacyCustomize.JsNumber(Num(texto["alturaLinha"], 1.2, 2.2, 1.7)));
        Set("--ui-editor-width", Widths.FirstOrDefault(x => x.Id == Str(editor, "largura", "media")).Width ?? Widths[1].Width);
        var attrs = new Dictionary<string, string>
        {
            ["data-tema"] = light ? "claro" : "escuro", ["data-densidade"] = Str(forma, "densidade", "normal"),
            ["data-vidro"] = forma["vidro"] is JsonValue vv && vv.TryGetValue<bool>(out var vb) && !vb ? "nao" : "sim", ["data-animacoes"] = Str(c, "animacoes", "sistema")
        };
        return new Variables(v, attrs, light, k["fundo"]);
    }

    /// <summary>salvarTema(nome): the path and text of Personalização/temas/&lt;nome&gt;.json for the current palette.</summary>
    public static (string Path, string Text) ThemeFile(JsonObject config, string name, Func<string, SavedTheme?>? saved = null)
    {
        name = LegacyCustomize.JsTrim(name ?? "");
        if (name.Length == 0) throw new InvalidOperationException("Dê um nome ao tema.");
        var path = LegacyCustomize.ThemePath(Regex.Replace(name, @"[\\/:*?""<>|]", "-"));
        var pal = PaletteOf(config, saved);
        var cores = new JsonObject();
        foreach (var (key, _) in Tokens) cores[key] = pal.Colors[key];
        var theme = Str(config, "tema", "escuro");
        var data = new JsonObject
        {
            ["nome"] = name, ["claro"] = pal.Light,
            ["base"] = PresetById(theme) is not null ? theme : pal.Light ? "claro" : "escuro",
            ["cores"] = cores
        };
        return (path, LegacyCustomize.Stringify(data, 2) + "\n");
    }
}
