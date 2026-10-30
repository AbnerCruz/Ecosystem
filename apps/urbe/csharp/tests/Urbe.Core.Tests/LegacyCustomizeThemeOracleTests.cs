using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// Personalização (aparência e chão) against the unmodified 1.8.4-beta customize.js and pixel-art.js
/// run by Node (tools/customize-theme-oracle.mjs): paleta(), variaveis(), carregarTexturas(), the
/// ground UrbeArt holds after configure() and the chunk pixels painted with it.
/// </summary>
public sealed class LegacyCustomizeThemeOracleTests
{
    private const string Pack = "Personalização/texturas/p.json";

    private static string Grid16(string letters, string colors) =>
        "{\"cores\":" + colors + ",\"pixels\":[" + string.Join(",", Enumerable.Range(0, 16).Select(r => "\"" + string.Concat(Enumerable.Range(0, 16).Select(c => letters[(r * 3 + c) % letters.Length])) + "\"")) + "]}";

    private static (string? Text, JsonObject Files) Case(string? text, JsonObject? files = null) => (text, files ?? []);

    private static List<(string? Text, JsonObject Files)> Cases()
    {
        var list = new List<(string? Text, JsonObject Files)> { Case(null), Case("{}") };
        foreach (var p in LegacyCustomizeTheme.Presets) list.Add(Case("{\"tema\":\"" + p.Id + "\"}"));
        list.Add(Case("{\"tema\":\"escuro\",\"cores\":{\"fundo\":\"#ffffff\"}}"));
        list.Add(Case("{\"tema\":\"claro\",\"cores\":{\"fundo\":\"#101010\",\"destaque\":\"#ffd400\"}}"));
        list.Add(Case("{\"cores\":{\"destaque\":\"#123456\",\"perigo\":\"#fff\",\"texto\":\"#ABCDEF\",\"x\":\"#000\"}}"));
        list.Add(Case("{\"cores\":{\"destaque\":\"#ffffff\"}}"));
        list.Add(Case("{\"cores\":{\"fundo\":\"#777777\"}}"));
        var themes = new JsonObject
        {
            ["Personalização/temas/Claro base.json"] = "{\"nome\":\"X\",\"claro\":false,\"base\":\"claro\",\"cores\":{\"destaque\":\"#c0ffee\",\"fundo\":\"#zzz\",\"nada\":\"#fff\"}}",
            ["Personalização/temas/Sem base.json"] = "{\"claro\":true,\"cores\":{\"texto\":\"#333\"}}",
            ["Personalização/temas/Sem nada.json"] = "{\"cores\":{\"texto\":\"#333\"}}",
            ["Personalização/temas/Base escura.json"] = "{\"base\":\"oceano\",\"cores\":{\"destaque\":\"#ff00aa\"}}",
            ["Personalização/temas/ruim.json"] = "{nope"
        };
        foreach (var t in new[] { "Claro base", "Sem base", "Sem nada", "Base escura", "ruim", "Personalização/temas/Sem base.json" })
            list.Add(Case("{\"tema\":\"" + t + "\"}", themes));
        list.Add(Case("{\"tema\":\"Sem base\",\"cores\":{\"fundo\":\"#000\"}}", themes));
        foreach (var f in new[]
        {
            "{\"texto\":{\"fonte\":\"serifada\",\"fonteEditor\":\"mono\",\"tamanho\":12,\"tamanhoEditor\":28,\"alturaLinha\":2.2}}",
            "{\"texto\":{\"fonte\":\"Inter\",\"fonteEditor\":\"Fira Sans;{x}<\\\\\",\",\"tamanho\":16.5,\"tamanhoEditor\":14.2,\"alturaLinha\":1.25}}",
            "{\"texto\":{\"fonte\":\"manuscrita\",\"fonteEditor\":\"leitura\",\"tamanho\":22,\"tamanhoEditor\":13}}",
            "{\"forma\":{\"cantos\":\"retos\",\"densidade\":\"compacta\",\"vidro\":false},\"animacoes\":\"reduzidas\",\"editor\":{\"largura\":\"estreita\"}}",
            "{\"forma\":{\"cantos\":\"redondos\",\"densidade\":\"confortavel\"},\"animacoes\":\"ligadas\",\"editor\":{\"largura\":\"total\"}}",
            "{\"editor\":{\"largura\":\"larga\"}}"
        }) list.Add(Case(f));

        string cores = "{\"a\":\"#ff0000\",\"b\":\"#00ff00\",\"c\":\"#0000ff\"}";
        JsonObject Pk(string json) => new() { [Pack] = json };
        string pack(string body) => "{" + body + "}";
        string sel = "{\"cidade\":{\"texturas\":\"" + Pack + "\"";
        list.Add(Case(sel + "}}", Pk(pack("\"chao\":{\"grama\":" + Grid16("abc.", cores) + "}"))));
        list.Add(Case(sel + "}}", Pk(pack("\"paleta\":{\"grama\":[\"#336699\"],\"água\":\"#abc\",\"deserto\":[\"#111\",\"#222\",\"#333\",\"#444\",\"#555\",\"zzz\"],\"foo\":[\"#fff\"],\"mar\":[\"x\"]}"))));
        list.Add(Case(sel + ",\"paleta\":{\"grama\":[\"#aa5500\"],\"neve\":[\"#101010\",\"#202020\",\"#303030\",\"#404040\"]}}}", Pk(pack("\"paleta\":{\"grama\":[\"#336699\"],\"praia\":[\"#999999\"]}"))));
        list.Add(Case(sel + "}}", Pk(pack("\"chao\":{\"agua\":[" + Grid16("ab", cores) + "," + Grid16("bc.", cores) + "," + Grid16("ca", cores) + "," + Grid16("a", cores) + "," + Grid16("b", cores) + "],\"floresta\":" + Grid16("..a", cores) + ",\"desconhecido\":" + Grid16("a", cores) + ",\"rio\":{\"nada\":1},\"lago\":{\"pixels\":[]},\"mata\":{\"pixels\":\"x\"},\"pico\":{\"imagem\":\"http://x/y.png\"}}"))));
        list.Add(Case(sel + "}}", Pk(pack("\"chao\":{\"taiga\":{\"cores\":{\"a\":\"#123456\",\"b\":\"#fedcba\"},\"pixels\":[\"ab\",\"ba\"]},\"tundra\":{\"cores\":{\"a\":\"#123456\"},\"pixels\":[\"aaaabbbb\",\"aaaabbbb\",\"aaaabbbb\",\"aaaabbbb\",\"bbbbaaaa\",\"bbbbaaaa\",\"bbbbaaaa\",\"bbbbaaaa\"]}}"))));
        var big = "{\"cores\":{\"a\":\"#335577\",\"b\":\"#aabbcc\"},\"pixels\":[" + string.Join(",", Enumerable.Range(0, 56).Select(r => "\"" + string.Concat(Enumerable.Range(0, 48).Select(c => (r / 4 + c / 4) % 3 == 0 ? 'a' : (r / 7 + c / 5) % 2 == 0 ? 'b' : '.')) + "\"")) + "]}";
        list.Add(Case(sel + "}}", Pk(pack("\"construcoes\":{\"casa\":" + big + ",\"torre\":[" + big + "],\"armazém\":{\"cores\":{\"a\":\"#fff\"},\"pixels\":[\"a.\",\".a\"]},\"nave\":" + big + ",\"salao\":{},\"mercado\":{\"imagem\":\"data:text/plain;base64,AA==\"}}"))));
        list.Add(Case(sel + "}}", Pk("{\"chao\":{\"grama\":" + Grid16("abc", cores) + "},\"construcoes\":{\"oficina\":" + big + "}}")));
        list.Add(Case(sel + "}}", Pk("{nope")));
        list.Add(Case("{\"cidade\":{\"texturas\":\"nao-existe.json\"}}"));
        return list;
    }

    private static string Locate(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, relative)) || Directory.Exists(Path.Combine(dir.FullName, relative))) return Path.Combine(dir.FullName, relative);
        throw new FileNotFoundException(relative);
    }

    private static int Mod(int v, int m) => (v % m + m) % m;

    [Fact]
    public async Task PaletteVariablesAndSavedThemesMatchTheOriginal()
    {
        var cases = Cases();
        var input = new JsonArray(cases.Select(c => (JsonNode)new JsonObject { ["text"] = c.Text, ["files"] = c.Files.DeepClone() }).ToArray());
        var start = new ProcessStartInfo("node") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Locate(Path.Combine("csharp", "tools", "customize-theme-oracle.mjs")));
        start.ArgumentList.Add(Path.GetDirectoryName(Path.GetDirectoryName(Locate(Path.Combine("src", "world", "pixel-art.js"))))!);
        using var process = Process.Start(start)!;
        var token = TestContext.Current.CancellationToken;
        await process.StandardInput.WriteAsync(input.ToJsonString());
        process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEndAsync(token);
        var stderr = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        Assert.True(process.ExitCode == 0, "Oráculo JS falhou: " + await stderr);
        using var json = JsonDocument.Parse(await stdout);
        Assert.Equal(cases.Count, json.RootElement.GetArrayLength());

        int i = 0;
        foreach (var e in json.RootElement.EnumerateArray())
        {
            var (text, files) = cases[i];
            var at = $"caso {i++}: {text}";
            LegacyCustomizeTheme.SavedTheme? Saved(string name)
            {
                var path = LegacyCustomize.ThemePath(name);
                return files[path] is JsonValue v && v.TryGetValue<string>(out var t)
                    ? LegacyCustomizeTheme.ParseSavedTheme(path, path.Split('/').Last(), t) : null;
            }
            bool SavedOk(string p) => files[p] is JsonValue v && v.TryGetValue<string>(out var t) && Try(() => LegacyCustomize.ParseJs(t)) is JsonObject;
            var n = LegacyCustomize.Normalize(text, SavedOk);
            var config = n.Config;
            Assert.Equal(e.GetProperty("config").GetString(), LegacyCustomize.Stringify(config));

            // paleta
            var pal = LegacyCustomizeTheme.PaletteOf(config, Saved);
            var ep = e.GetProperty("palette");
            Assert.True(ep.GetProperty("claro").GetBoolean() == pal.Light, at + " claro");
            var ec = ep.GetProperty("cores").EnumerateObject().ToDictionary(p => p.Name, p => NativeContrast.Native(p.Value.GetString()));
            Assert.Equal(ec.OrderBy(k => k.Key).Select(k => k.Key + k.Value), pal.Colors.OrderBy(k => k.Key).Select(k => k.Key + k.Value));

            // variaveis
            var vars = LegacyCustomizeTheme.VariablesOf(config, Saved);
            var ev = e.GetProperty("variables");
            Assert.Equal(ev.GetProperty("vars").EnumerateObject().Select(p => p.Name + "=" + NativeContrast.Native(p.Value.GetString())), vars.Vars.Select(p => p.Key + "=" + p.Value));
            Assert.Equal(ev.GetProperty("attrs").EnumerateObject().Select(p => p.Name + "=" + p.Value.GetString()).OrderBy(x => x), vars.Attrs.Select(p => p.Key + "=" + p.Value).OrderBy(x => x));
            Assert.Equal(ev.GetProperty("claro").GetBoolean(), vars.Light);
            Assert.Equal(ev.GetProperty("fundo").GetString(), vars.Background);

            var (themePath, themeText) = LegacyCustomizeTheme.ThemeFile(config, "Tema \"x\"/y é", Saved);
            Assert.Equal(e.GetProperty("savedTheme").GetProperty("path").GetString(), themePath);
            Assert.Equal(NativeContrast.Native(e.GetProperty("savedTheme").GetProperty("content").GetString()), themeText);
        }
    }

    private static JsonNode? Try(Func<JsonNode?> f)
    {
        try { return f(); } catch (JsonException) { return null; }
    }
}

/// <summary>
/// CST-105 / DEC-0059: the 1.8.4's texto3 of each ready-made theme read at 2.5–3.9:1; the native app's reads at WCAG AA
/// (ThemeContrastTests). The oracle's output becomes the native one with exactly these replacements; nothing else may drift.
/// </summary>
internal static class NativeContrast
{
    private static readonly (string Original, string Native)[] Texto3 =
    [
        ("#6d7079", "#898c95"),
        ("#6c7d69", "#879883"),
        ("#62818a", "#7b9aa2"),
        ("#86686f", "#9f8288"),
        ("#8a8d97", "#656872"),
        ("#9a876d", "#6e5e4a"),
        ("#6b6f96", "#868aaf")
    ];

    public static string? Native(string? value)
    {
        if (value is null) return null;
        foreach (var (from, to) in Texto3) value = value.Replace(from, to, StringComparison.OrdinalIgnoreCase);
        return value;
    }
}
