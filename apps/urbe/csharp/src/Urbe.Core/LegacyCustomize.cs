using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urbe.Core;

/// <summary>
/// src/customize/customize.js (1.8.4-beta): the configuration kept in the vault file
/// Personalização/tema.json. Ported line by line: DEFAULT, normalize (clean configuration,
/// errors and warnings per path, in the original wording and order), merge/set and the file
/// text that keeps only what differs from the default (semPadroes). The configuration is a
/// JSON object with JavaScript property order; serialization follows JSON.stringify.
/// </summary>
public static class LegacyCustomize
{
    public const string Folder = "Personalização";
    public const string FilePath = Folder + "/tema.json";

    public static readonly string[] Presets = ["escuro", "meia-noite", "floresta", "oceano", "vinho", "claro", "sepia", "alto-contraste"];
    public static readonly string[] Colors = ["fundo", "superficie", "superficie2", "superficie3", "linha", "linha2", "texto", "texto2", "texto3", "destaque", "perigo", "sucesso", "aviso", "codigo"];
    public static readonly string[] Fonts = ["sistema", "arredondada", "humanista", "serifada", "leitura", "mono", "manuscrita"];
    public static readonly string[] Corners = ["retos", "suaves", "redondos"];
    public static readonly string[] Widths = ["estreita", "media", "larga", "total"];
    public static readonly string[] Ambients = ["dia", "entardecer", "noite", "auto", "ciclo"];

    // BIOMAS / GRUPOS / BIOMAS_NOME
    private static readonly Dictionary<string, string> BiomeAliases = new(StringComparer.Ordinal)
    {
        ["grama"] = "grass", ["campo"] = "grass", ["prado"] = "meadow", ["floresta"] = "forest", ["mata"] = "dense", ["mata-fechada"] = "dense",
        ["pantano"] = "swamp", ["pântano"] = "swamp", ["taiga"] = "taiga", ["tundra"] = "tundra", ["neve"] = "snow", ["colinas"] = "hills",
        ["montanha"] = "mountain", ["pico"] = "peak", ["deserto"] = "desert", ["savana"] = "savanna", ["estepe"] = "steppe", ["praia"] = "beach",
        ["mar"] = "sea", ["profundo"] = "deep", ["oceano"] = "deep", ["rio"] = "river", ["lago"] = "lake"
    };
    private static readonly string[] BiomeIds = ["grass", "meadow", "forest", "dense", "swamp", "taiga", "tundra", "snow", "hills", "mountain", "peak", "desert", "savanna", "steppe", "beach", "sea", "deep", "river", "lake"];
    private static readonly string[] Water = ["river", "lake", "sea", "deep"];
    private static readonly string[] ObjectPrototype =
        ["constructor", "__proto__", "__defineGetter__", "__defineSetter__", "__lookupGetter__", "__lookupSetter__", "hasOwnProperty",
         "isPrototypeOf", "propertyIsEnumerable", "toString", "valueOf", "toLocaleString"];

    public sealed record Issue(string Path, string Message);
    public sealed record Result(JsonObject Config, IReadOnlyList<Issue> Errors, IReadOnlyList<Issue> Warnings);

    /// <summary>DEFAULT (a fresh clone each time).</summary>
    public static JsonObject Default() => new()
    {
        ["versao"] = 1, ["tema"] = "escuro", ["cores"] = new JsonObject(),
        ["texto"] = new JsonObject { ["fonte"] = "sistema", ["fonteEditor"] = "sistema", ["tamanho"] = 15, ["tamanhoEditor"] = 17, ["alturaLinha"] = 1.7 },
        ["forma"] = new JsonObject { ["cantos"] = "suaves", ["densidade"] = "normal", ["vidro"] = true },
        ["animacoes"] = "sistema",
        ["editor"] = new JsonObject { ["largura"] = "media", ["modoInicial"] = "visual", ["ortografia"] = true },
        ["cidade"] = new JsonObject
        {
            ["moradores"] = true, ["fauna"] = true, ["clima"] = true, ["eventos"] = true, ["nomes"] = true, ["bairros"] = true,
            ["ambiente"] = "ciclo", ["texturas"] = "", ["paleta"] = new JsonObject()
        },
        ["estilos"] = new JsonObject(), ["css"] = ""
    };

    /// <summary>biomaId(k): engine ids for a Portuguese (or engine) biome name, a group, or null.</summary>
    public static string[]? BiomeId(string? k)
    {
        k = JsTrim((k ?? "").ToLowerInvariant());
        if (BiomeIds.Contains(k) || ObjectPrototype.Contains(k)) return [k];   // BIOMAS_NOME[k] (prototype names are truthy in JS)
        if (BiomeAliases.TryGetValue(k, out var id)) return [id];
        if (k is "agua" or "água") return [.. Water];
        return null;
    }

    // ---------------------------------------------------------------- normalize

    /// <summary>normalize(input): <paramref name="savedTheme"/> answers temaSalvo(nome) (a valid saved theme exists).</summary>
    public static Result Normalize(string? text, Func<string, bool>? savedTheme = null)
    {
        JsonNode? src;
        if (text is null) src = new JsonObject();
        else if (JsTrim(text).Length == 0) src = new JsonObject();
        else
        {
            try { src = ParseJs(text); }
            catch (JsonException e) { return new Result(Default(), [new Issue("", "JSON inválido: " + e.Message)], []); }
        }
        return Normalize(src, savedTheme);
    }

    public static Result Normalize(JsonNode? src, Func<string, bool>? savedTheme = null)
    {
        var errors = new List<Issue>();
        var warnings = new List<Issue>();
        if (src is not JsonObject s) s = new JsonObject();
        var c = Default();
        void Known(JsonObject obj, string @base, IEnumerable<string> keys)
        {
            var list = keys.ToList();
            foreach (var k in Keys(obj))
                if (!list.Contains(k)) warnings.Add(new Issue(@base + k, "propriedade desconhecida (ignorada)"));
        }
        Known(s, "", Keys(Default()).Concat(["$schema", "descricao", "descrição"]));
        if (Get(s, "tema") is { } tema)
        {
            if (tema is JsonValue tv && tv.TryGetValue<string>(out var ts) && JsTrim(ts).Length > 0) c["tema"] = JsTrim(ts);
            else errors.Add(new Issue("tema", "use o nome de um tema pronto (" + string.Join(", ", Presets) + ") ou o caminho de um tema salvo"));
        }
        if (Get(s, "cores") is { } cores)
        {
            if (cores is not JsonObject co) errors.Add(new Issue("cores", "deve ser um objeto {nome: \"#rrggbb\"}"));
            else
            {
                Known(co, "cores.", Colors);
                foreach (var k in Colors)
                {
                    var v = Get(co, k);
                    if (v is null || (v is JsonValue sv && sv.TryGetValue<string>(out var str) && str.Length == 0)) continue;
                    if (Hex(v) is { } h) ((JsonObject)c["cores"]!)[k] = h;
                    else errors.Add(new Issue("cores." + k, "cor inválida \"" + JsToString(v) + "\" (use #rrggbb)"));
                }
            }
        }
        void Sec(string name, Action<JsonObject> fn)
        {
            var o = Get(s, name);
            if (o is null) return;
            if (o is not JsonObject obj) { errors.Add(new Issue(name, "deve ser um objeto")); return; }
            fn(obj);
        }
        void Pick(JsonObject o, string @base, string key, string[] list)
        {
            var v = Get(o, key);
            if (v is null) return;
            if (v is JsonValue jv && jv.TryGetValue<string>(out var str) && list.Contains(str)) ((JsonObject)c[@base]!)[key] = str;
            else errors.Add(new Issue(@base + "." + key, "use um de: " + string.Join(", ", list)));
        }
        void Bool(JsonObject o, string @base, string key)
        {
            var v = Get(o, key);
            if (v is null) return;
            if (v is JsonValue jv && jv.TryGetValue<bool>(out var b)) ((JsonObject)c[@base]!)[key] = b;
            else errors.Add(new Issue(@base + "." + key, "use true ou false"));
        }
        void Range(JsonObject o, string @base, string key, double min, double max)
        {
            var v = Get(o, key);
            if (v is null) return;
            double n = JsToNumber(v);
            if (double.IsFinite(n) && n >= min && n <= max) ((JsonObject)c[@base]!)[key] = n;
            else errors.Add(new Issue(@base + "." + key, "número entre " + JsNumber(min) + " e " + JsNumber(max)));
        }
        Sec("texto", o =>
        {
            Known(o, "texto.", Keys((JsonObject)Default()["texto"]!));
            foreach (var k in new[] { "fonte", "fonteEditor" })
            {
                var v = Get(o, k);
                if (v is null) continue;
                if (v is JsonValue jv && jv.TryGetValue<string>(out var str) && JsTrim(str).Length > 0)
                {
                    ((JsonObject)c["texto"]!)[k] = JsTrim(str);
                    if (!Fonts.Contains(str) && !ObjectPrototype.Contains(str))
                        warnings.Add(new Issue("texto." + k, "fonte própria: \"" + str + "\" — precisa estar instalada no aparelho ou declarada num estilo CSS"));
                }
                else errors.Add(new Issue("texto." + k, "use " + string.Join(", ", Fonts) + " ou o nome de uma fonte"));
            }
            Range(o, "texto", "tamanho", 12, 22);
            Range(o, "texto", "tamanhoEditor", 13, 28);
            Range(o, "texto", "alturaLinha", 1.2, 2.2);
        });
        Sec("forma", o =>
        {
            Known(o, "forma.", Keys((JsonObject)Default()["forma"]!));
            Pick(o, "forma", "cantos", Corners);
            Pick(o, "forma", "densidade", ["compacta", "normal", "confortavel"]);
            Bool(o, "forma", "vidro");
        });
        if (Get(s, "animacoes") is { } an)
        {
            if (an is JsonValue av && av.TryGetValue<string>(out var a) && a is "sistema" or "ligadas" or "reduzidas") c["animacoes"] = a;
            else errors.Add(new Issue("animacoes", "use sistema, ligadas ou reduzidas"));
        }
        Sec("editor", o =>
        {
            Known(o, "editor.", Keys((JsonObject)Default()["editor"]!));
            Pick(o, "editor", "largura", Widths);
            Pick(o, "editor", "modoInicial", ["visual", "texto"]);
            Bool(o, "editor", "ortografia");
        });
        Sec("cidade", o =>
        {
            Known(o, "cidade.", Keys((JsonObject)Default()["cidade"]!));
            foreach (var k in new[] { "moradores", "fauna", "clima", "eventos", "nomes", "bairros" }) Bool(o, "cidade", k);
            Pick(o, "cidade", "ambiente", Ambients);
            if (Get(o, "texturas") is { } tx)
            {
                if (tx is JsonValue t && t.TryGetValue<string>(out var str)) ((JsonObject)c["cidade"]!)["texturas"] = JsTrim(str);
                else errors.Add(new Issue("cidade.texturas", "caminho do pacote de texturas (texto) ou \"\" para nenhum"));
            }
            if (Get(o, "paleta") is { } pa)
            {
                if (pa is not JsonObject po) errors.Add(new Issue("cidade.paleta", "objeto {bioma: [\"#base\", \"#sombra\", \"#luz\", \"#detalhe\"]}"));
                else
                    foreach (var k in Keys(po))
                    {
                        var ids = BiomeId(k);
                        var v = Get(po, k);
                        if (ids is null)
                        {
                            errors.Add(new Issue("cidade.paleta." + k, "bioma desconhecido; use " +
                                string.Join(", ", BiomeAliases.Keys.Concat(["agua", "água"]))));
                            continue;
                        }
                        var items = v is JsonArray arr ? arr.ToList() : [v];
                        var hexes = items.Select(Hex).ToList();
                        if (hexes.Count == 0 || hexes.Any(x => x is null))
                        {
                            errors.Add(new Issue("cidade.paleta." + k, "lista de 1 a 4 cores #rrggbb"));
                            continue;
                        }
                        ((JsonObject)((JsonObject)c["cidade"]!)["paleta"]!)[k] = new JsonArray(hexes.Take(4).Select(x => (JsonNode)JsonValue.Create(x!)).ToArray());
                    }
            }
        });
        if (Get(s, "estilos") is { } es)
        {
            if (es is not JsonObject eo) errors.Add(new Issue("estilos", "objeto {\"Personalização/estilos/arquivo.css\": true}"));
            else
                foreach (var k in Keys(eo))
                {
                    if (Get(eo, k) is JsonValue bv && bv.TryGetValue<bool>(out var b)) ((JsonObject)c["estilos"]!)[k] = b;
                    else errors.Add(new Issue("estilos." + k, "use true ou false"));
                }
        }
        if (Get(s, "css") is { } css)
        {
            if (css is JsonValue cv && cv.TryGetValue<string>(out var str)) c["css"] = str.Length > 40000 ? str[..40000] : str;
            else errors.Add(new Issue("css", "texto com regras CSS"));
        }
        var theme = (string)c["tema"]!;
        if (!Presets.Contains(theme) && !ObjectPrototype.Contains(theme) && !SavedTheme(theme, savedTheme))
            warnings.Add(new Issue("tema", "tema \"" + theme + "\" não encontrado; usando Escuro"));
        return new Result(c, errors, warnings);
    }

    private static bool SavedTheme(string name, Func<string, bool>? savedTheme) => savedTheme?.Invoke(ThemePath(name)) ?? false;

    /// <summary>caminhoTema(nome).</summary>
    public static string ThemePath(string name)
    {
        var p = JsTrim(name ?? "");
        if (p.Length == 0) return "";
        if (!p.Contains('/')) p = Folder + "/temas/" + p;
        if (!p.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) p += ".json";
        return p;
    }

    // ---------------------------------------------------------------- set / file

    /// <summary>merge(a, b): b's values over a copy of a, objects merged recursively.</summary>
    public static JsonObject Merge(JsonObject a, JsonObject? b)
    {
        var o = (JsonObject)a.DeepClone();
        if (b is null) return o;
        foreach (var k in Keys(b))
        {
            var bv = Get(b, k);
            if (bv is JsonObject bo && Get(o, k) is JsonObject ao) o[k] = Merge(ao, bo);
            else o[k] = bv?.DeepClone();
        }
        return o;
    }

    /// <summary>The text gravar() writes to tema.json: $schema and only what differs from DEFAULT.</summary>
    public static string FileText(JsonObject config)
    {
        var defaults = Default();
        var output = new JsonObject { ["$schema"] = "urbe-tema-1" };
        foreach (var k in Keys(config))
        {
            if (k == "versao") { output["versao"] = 1; continue; }
            var a = Get(config, k);
            var b = Get(defaults, k);
            if (a is JsonObject ao && b is JsonObject bo)
            {
                var o = new JsonObject();
                foreach (var x in Keys(ao))
                    if (Stringify(Get(ao, x)) != Stringify(Get(bo, x))) o[x] = Get(ao, x)?.DeepClone();
                if (o.Count > 0) output[k] = o;
            }
            else if (Stringify(a) != Stringify(b)) output[k] = a?.DeepClone();
        }
        return Stringify(output, 2) + "\n";
    }

    // ---------------------------------------------------------------- JavaScript semantics

    /// <summary>JSON.parse: duplicate keys keep the first position and the last value.</summary>
    public static JsonNode? ParseJs(string text)
    {
        using var doc = JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
        return Convert(doc.RootElement);
    }

    private static JsonNode? Convert(JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                var keys = new List<string>();
                var values = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
                foreach (var p in e.EnumerateObject())
                {
                    if (!values.ContainsKey(p.Name)) keys.Add(p.Name);
                    values[p.Name] = Convert(p.Value);
                }
                var o = new JsonObject();
                foreach (var k in OrderKeys(keys)) o[k] = values[k];
                return o;
            case JsonValueKind.Array:
                return new JsonArray(e.EnumerateArray().Select(Convert).ToArray());
            case JsonValueKind.String: return JsonValue.Create(e.GetString());
            case JsonValueKind.Number: return JsonValue.Create(double.Parse(e.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture));
            case JsonValueKind.True: return JsonValue.Create(true);
            case JsonValueKind.False: return JsonValue.Create(false);
            default: return null;
        }
    }

    /// <summary>Object.keys order: integer indices ascending, then strings in insertion order.</summary>
    private static IEnumerable<string> OrderKeys(IEnumerable<string> keys)
    {
        var list = keys.ToList();
        static bool Index(string k, out uint n) => uint.TryParse(k, NumberStyles.None, CultureInfo.InvariantCulture, out n) && n < uint.MaxValue && n.ToString(CultureInfo.InvariantCulture) == k;
        return list.Where(k => Index(k, out _)).OrderBy(k => uint.Parse(k, CultureInfo.InvariantCulture)).Concat(list.Where(k => !Index(k, out _)));
    }

    public static IEnumerable<string> Keys(JsonObject o) => OrderKeys(o.Select(p => p.Key));

    private static JsonNode? Get(JsonObject o, string key) => o.TryGetPropertyValue(key, out var v) ? v : null;

    /// <summary>hex(c): #rgb → #rrggbb, #rrggbb lowercased, anything else null.</summary>
    public static string? Hex(JsonNode? v)
    {
        if (v is not JsonValue jv || !jv.TryGetValue<string>(out var c)) return null;
        c = JsTrim(c);
        if (System.Text.RegularExpressions.Regex.IsMatch(c, "^#[0-9a-fA-F]{3}$"))
            return ("#" + string.Concat(c[1..].Select(x => new string(x, 2)))).ToLowerInvariant();
        return System.Text.RegularExpressions.Regex.IsMatch(c, "^#[0-9a-fA-F]{6}$") ? c.ToLowerInvariant() : null;
    }

    /// <summary>String.prototype.trim (WhiteSpace + LineTerminator).</summary>
    public static string JsTrim(string s) => s.Trim(TrimChars);

    private static readonly char[] TrimChars = ['\u0009', '\u000B', '\u000C', '\u0020', '\u00A0', '\uFEFF', '\u000A', '\u000D', '\u2028', '\u2029', '\u1680', '\u2000', '\u2001', '\u2002', '\u2003', '\u2004', '\u2005', '\u2006', '\u2007', '\u2008', '\u2009', '\u200A', '\u202F', '\u205F', '\u3000'];

    /// <summary>String(v) for a JSON value.</summary>
    public static string JsToString(JsonNode? v) => v switch
    {
        null => "null",
        JsonObject => "[object Object]",
        JsonArray a => string.Join(",", a.Select(x => x is null ? "" : JsToString(x))),
        JsonValue jv when jv.TryGetValue<string>(out var s) => s,
        JsonValue jv when jv.TryGetValue<bool>(out var b) => b ? "true" : "false",
        JsonValue jv when jv.TryGetValue<double>(out var d) => JsNumber(d),
        JsonValue jv when jv.TryGetValue<int>(out var i) => JsNumber(i),
        _ => v.ToJsonString()
    };

    /// <summary>Number(v) for a JSON value.</summary>
    public static double JsToNumber(JsonNode? v)
    {
        switch (v)
        {
            case null: return 0;
            case JsonObject: return double.NaN;
            case JsonArray: return StringToNumber(JsToString(v));
            case JsonValue jv when jv.TryGetValue<bool>(out var b): return b ? 1 : 0;
            case JsonValue jv when jv.TryGetValue<string>(out var s): return StringToNumber(s);
            case JsonValue jv when jv.TryGetValue<double>(out var d): return d;
            case JsonValue jv when jv.TryGetValue<int>(out var i): return i;
            default: return double.NaN;
        }
    }

    private static double StringToNumber(string s)
    {
        s = JsTrim(s);
        if (s.Length == 0) return 0;
        if (s.Length > 2 && s[0] == '0' && (s[1] | 0x20) is 'x' or 'o' or 'b')
        {
            int radix = (s[1] | 0x20) switch { 'x' => 16, 'o' => 8, _ => 2 };
            double n = 0;
            foreach (var ch in s[2..])
            {
                int d = char.IsAsciiDigit(ch) ? ch - '0' : char.IsAsciiLetter(ch) ? (ch | 0x20) - 'a' + 10 : 99;
                if (d >= radix) return double.NaN;
                n = n * radix + d;
            }
            return n;
        }
        if (s is "Infinity" or "+Infinity") return double.PositiveInfinity;
        if (s == "-Infinity") return double.NegativeInfinity;
        if (!System.Text.RegularExpressions.Regex.IsMatch(s, @"^[+-]?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?$")) return double.NaN;
        return double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    /// <summary>Number::toString (shortest round-trip, ECMAScript notation).</summary>
    public static string JsNumber(double v)
    {
        if (double.IsNaN(v)) return "NaN";
        if (double.IsPositiveInfinity(v)) return "Infinity";
        if (double.IsNegativeInfinity(v)) return "-Infinity";
        if (v == 0) return "0";
        if (v < 0) return "-" + JsNumber(-v);
        var r = v.ToString("E16", CultureInfo.InvariantCulture);
        // shortest digits that round-trip
        string digits = "";
        int exp = 0;
        for (int p = 1; p <= 17; p++)
        {
            var e = v.ToString("E" + (p - 1), CultureInfo.InvariantCulture);
            if (double.Parse(e, CultureInfo.InvariantCulture) == v)
            {
                var mant = e[..e.IndexOf('E')].Replace(".", "");
                exp = int.Parse(e[(e.IndexOf('E') + 1)..], CultureInfo.InvariantCulture);
                digits = mant.TrimEnd('0');
                if (digits.Length == 0) digits = "0";
                break;
            }
        }
        _ = r;
        int k = digits.Length, n = exp + 1;
        if (k <= n && n <= 21) return digits + new string('0', n - k);
        if (0 < n && n <= 21) return digits[..n] + "." + digits[n..];
        if (-6 < n && n <= 0) return "0." + new string('0', -n) + digits;
        var sign = n - 1 >= 0 ? "+" : "-";
        var e1 = Math.Abs(n - 1).ToString(CultureInfo.InvariantCulture);
        return k == 1 ? digits + "e" + sign + e1 : digits[0] + "." + digits[1..] + "e" + sign + e1;
    }

    /// <summary>JSON.stringify(value, null, indent).</summary>
    public static string Stringify(JsonNode? node, int indent = 0)
    {
        var sb = new StringBuilder();
        Write(sb, node, indent, "");
        return sb.ToString();
    }

    private static void Write(StringBuilder sb, JsonNode? node, int indent, string current)
    {
        switch (node)
        {
            case null: sb.Append("null"); return;
            case JsonObject o:
            {
                var keys = Keys(o).ToList();
                if (keys.Count == 0) { sb.Append("{}"); return; }
                var inner = current + new string(' ', indent);
                sb.Append('{');
                for (int i = 0; i < keys.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    if (indent > 0) sb.Append('\n').Append(inner);
                    Quote(sb, keys[i]);
                    sb.Append(indent > 0 ? ": " : ":");
                    Write(sb, Get(o, keys[i]), indent, inner);
                }
                if (indent > 0) sb.Append('\n').Append(current);
                sb.Append('}');
                return;
            }
            case JsonArray a:
            {
                if (a.Count == 0) { sb.Append("[]"); return; }
                var inner = current + new string(' ', indent);
                sb.Append('[');
                for (int i = 0; i < a.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    if (indent > 0) sb.Append('\n').Append(inner);
                    Write(sb, a[i], indent, inner);
                }
                if (indent > 0) sb.Append('\n').Append(current);
                sb.Append(']');
                return;
            }
            case JsonValue v when v.TryGetValue<string>(out var s): Quote(sb, s); return;
            case JsonValue v when v.TryGetValue<bool>(out var b): sb.Append(b ? "true" : "false"); return;
            default:
            {
                double d = JsToNumber(node);
                sb.Append(double.IsFinite(d) ? JsNumber(d) : "null");
                return;
            }
        }
    }

    private static void Quote(StringBuilder sb, string s)
    {
        sb.Append('"');
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    bool lone = char.IsHighSurrogate(c) ? !(i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                        : char.IsLowSurrogate(c) && !(i > 0 && char.IsHighSurrogate(s[i - 1]));
                    if (c < 0x20 || lone) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }
}
