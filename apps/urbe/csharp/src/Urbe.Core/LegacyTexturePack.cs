using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// carregarTexturas(c) of src/customize/customize.js: a texture pack JSON
/// (Personalização/texturas/*.json) becomes the biome palette, the 16×16 ground textures and the
/// building images the world draws. Pixel grids are decoded here; embedded images are decoded by
/// the client (<c>decode</c>), then resampled like canvas drawImage.
/// </summary>
public static class LegacyTexturePack
{
    public sealed record DecodedImage(byte[] Rgba, int Width, int Height);
    /// <summary>A building image: RGBA of any size up to 64×64 (grid) or as decoded.</summary>
    public sealed record Art(byte[] Rgba, int Width, int Height);

    public sealed class Result
    {
        /// <summary>out.paleta: biome id → valid colours.</summary>
        public Dictionary<string, string[]> Palette { get; } = [];
        /// <summary>out.chao: biome id → up to four 16×16 RGBA images.</summary>
        public Dictionary<string, List<byte[]>> Ground { get; } = [];
        /// <summary>out.construcoes: building kind → image.</summary>
        public Dictionary<string, Art> Buildings { get; } = [];
        public List<string> Errors { get; } = [];
    }

    private static readonly Dictionary<string, string> BuildingAliases = new(StringComparer.Ordinal)
    {
        ["casa"] = "house", ["salao"] = "hall", ["salão"] = "hall", ["oficina"] = "workshop", ["tinturaria"] = "dyer",
        ["torre"] = "tower", ["mercado"] = "market", ["armazem"] = "store", ["armazém"] = "store", ["loja"] = "store"
    };
    private static readonly string[] BuildingIds = ["house", "hall", "workshop", "dyer", "tower", "market", "store"];

    /// <summary>construcaoId(k).</summary>
    public static string? BuildingId(string? k)
    {
        k = LegacyCustomize.JsTrim((k ?? "").ToLowerInvariant());
        if (BuildingIds.Contains(k)) return k;
        return BuildingAliases.GetValueOrDefault(k);
    }

    private static readonly Regex DataImage = new(@"^data:image/(png|jpe?g|gif|webp|svg\+xml);base64,", RegexOptions.IgnoreCase);

    /// <summary>
    /// Loads a pack. <paramref name="baseOf"/> is A.PAL[id][0] at the time of loading (the colour a "."
    /// pixel takes); <paramref name="decode"/> turns an embedded image into RGBA (null = cannot).
    /// </summary>
    public static Result Load(string? content, Func<string, string> baseOf, Func<byte[], DecodedImage?>? decode)
    {
        var o = new Result();
        if (content is null) return o;
        JsonNode? parsed;
        try { parsed = LegacyCustomize.ParseJs(content); }
        catch (System.Text.Json.JsonException e) { o.Errors.Add("JSON inválido: " + e.Message); return o; }
        var data = parsed as JsonObject ?? [];

        if (data["paleta"] is JsonObject pal)
            foreach (var k in LegacyCustomize.Keys(pal))
                foreach (var id in LegacyCustomize.BiomeId(k) ?? [])
                {
                    var arr = (pal[k] is JsonArray ja ? ja.AsEnumerable() : [pal[k]]).Select(LegacyCustomize.Hex).Where(h => h is not null).Select(h => h!).ToArray();
                    if (arr.Length > 0) o.Palette[id] = arr;
                }

        if (data["chao"] is JsonObject chao)
            foreach (var k in LegacyCustomize.Keys(chao))
            {
                var ids = LegacyCustomize.BiomeId(k);
                if (ids is null) { o.Errors.Add("chao." + k + ": bioma desconhecido"); continue; }
                var list = new List<byte[]>();
                foreach (var spec in Variations(chao[k]))
                {
                    try
                    {
                        var img = Source(spec, baseOf(ids[0]), decode);
                        list.Add(Pixels16(img, img.Width > 16));
                    }
                    catch (InvalidOperationException e) { o.Errors.Add("chao." + k + ": " + e.Message); }
                }
                if (list.Count > 0) foreach (var id in ids) o.Ground[id] = list;
            }

        if (data["construcoes"] is JsonObject cons)
            foreach (var b in LegacyCustomize.Keys(cons))
            {
                var id = BuildingId(b);
                if (id is null) { o.Errors.Add("construcoes." + b + ": tipo desconhecido"); continue; }
                try
                {
                    var first = Variations(cons[b]).FirstOrDefault() ?? [];
                    var img = Source(first, null, decode);
                    o.Buildings[id] = new Art(img.Rgba, img.Width, img.Height);
                }
                catch (InvalidOperationException e) { o.Errors.Add("construcoes." + b + ": " + e.Message); }
            }
        return o;
    }

    /// <summary>variacoes(v): an object or a list of objects, at most four.</summary>
    private static IEnumerable<JsonObject> Variations(JsonNode? v) =>
        (v is JsonArray a ? a.AsEnumerable() : [v]).OfType<JsonObject>().Take(4);

    /// <summary>fonteDe(spec, padrao).</summary>
    private static DecodedImage Source(JsonObject spec, string? fallback, Func<byte[], DecodedImage?>? decode)
    {
        if (Truthy(spec["imagem"]))
        {
            var url = LegacyCustomize.JsToString(spec["imagem"]);
            var m = DataImage.Match(url);
            if (!m.Success) throw new InvalidOperationException("imagem precisa ser data:image/...;base64");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(url[m.Length..]); }
            catch (FormatException) { throw new InvalidOperationException("imagem inválida"); }
            return decode?.Invoke(bytes) ?? throw new InvalidOperationException("imagem inválida");
        }
        if (Truthy(spec["pixels"])) return Grid(spec, fallback);
        throw new InvalidOperationException("use \"imagem\" ou \"pixels\"");
    }

    private static bool Truthy(JsonNode? n) => n switch
    {
        null => false,
        JsonValue v when v.TryGetValue<bool>(out var b) => b,
        JsonValue v when v.TryGetValue<string>(out var s) => s.Length > 0,
        JsonValue v when v.TryGetValue<double>(out var d) => d != 0 && !double.IsNaN(d),
        _ => true
    };

    /// <summary>grade(spec, padrao): {cores:{a:"#.."}, pixels:["aab.."]} up to 64×64; "." and unknown letters take the fallback.</summary>
    private static DecodedImage Grid(JsonObject spec, string? fallback)
    {
        var rows = spec["pixels"] is JsonArray pa ? pa.Select(x => LegacyCustomize.JsToString(x)).ToArray() : [];
        if (rows.Length == 0) throw new InvalidOperationException("pixels vazio");
        int h = Math.Min(64, rows.Length), w = Math.Min(64, rows.Max(r => r.Length));
        if (w == 0) throw new InvalidOperationException("pixels vazio");
        var cores = spec["cores"] as JsonObject ?? [];
        var rgba = new byte[w * h * 4];
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                var ch = i < rows[j].Length ? rows[j][i].ToString() : ".";
                var c = cores.TryGetPropertyValue(ch, out var cv) ? LegacyCustomize.Hex(cv) : null;
                if (c is null) { if (fallback is not null) c = fallback; else continue; }
                var q = LegacyCustomizeTheme.Rgb(c);
                int o = (j * w + i) * 4;
                rgba[o] = (byte)q[0]; rgba[o + 1] = (byte)q[1]; rgba[o + 2] = (byte)q[2]; rgba[o + 3] = 255;
            }
        return new DecodedImage(rgba, w, h);
    }

    /// <summary>pixels16(fonte): drawImage into a 16×16 canvas (nearest below the size, bilinear when smoothing) and read back.</summary>
    public static byte[] Pixels16(DecodedImage src, bool smoothing)
    {
        const int N = LegacyWorldPixelTextures.TileSize;
        var d = new byte[N * N * 4];
        if (src.Width == N && src.Height == N) { Array.Copy(src.Rgba, d, d.Length); return d; }
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int o = (y * N + x) * 4;
                double sx = (x + .5) * src.Width / N, sy = (y + .5) * src.Height / N;
                if (!smoothing)
                {
                    int ix = Math.Min(src.Width - 1, (int)Math.Floor(sx)), iy = Math.Min(src.Height - 1, (int)Math.Floor(sy));
                    Array.Copy(src.Rgba, (iy * src.Width + ix) * 4, d, o, 4);
                    continue;
                }
                double fx = Math.Clamp(sx - .5, 0, src.Width - 1), fy = Math.Clamp(sy - .5, 0, src.Height - 1);
                int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy), x1 = Math.Min(src.Width - 1, x0 + 1), y1 = Math.Min(src.Height - 1, y0 + 1);
                double tx = fx - x0, ty = fy - y0;
                for (int c = 0; c < 4; c++)
                {
                    double a = src.Rgba[(y0 * src.Width + x0) * 4 + c], b = src.Rgba[(y0 * src.Width + x1) * 4 + c];
                    double e = src.Rgba[(y1 * src.Width + x0) * 4 + c], f = src.Rgba[(y1 * src.Width + x1) * 4 + c];
                    double top = a + (b - a) * tx, bottom = e + (f - e) * tx;
                    d[o + c] = (byte)Math.Clamp((int)Math.Floor(top + (bottom - top) * ty + .5), 0, 255);
                }
            }
        return d;
    }

    /// <summary>aplicarCidade: the palette (config paleta, then the pack's for biomes left) and the textures for configure.</summary>
    public static LegacyGroundStyle Style(JsonObject config, Result pack)
    {
        var pal = new Dictionary<string, IReadOnlyList<string>>();
        if (config["cidade"] is JsonObject city && city["paleta"] is JsonObject own)
            foreach (var k in LegacyCustomize.Keys(own))
                foreach (var id in LegacyCustomize.BiomeId(k) ?? [])
                    pal[id] = (own[k] as JsonArray ?? []).Select(x => LegacyCustomize.Hex(x)!).ToArray();
        foreach (var kv in pack.Palette) if (!pal.ContainsKey(kv.Key)) pal[kv.Key] = kv.Value;
        return LegacyGroundStyle.Configure(pal, pack.Ground.ToDictionary(k => k.Key, k => (IReadOnlyList<byte[]>)k.Value));
    }
}
