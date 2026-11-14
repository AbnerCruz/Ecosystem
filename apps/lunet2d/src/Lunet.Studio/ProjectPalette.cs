using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lunet.Studio;

/// <summary>One named color of the game's palette.</summary>
public sealed record PaletteSwatch(string Name, uint Argb);

public enum PaletteHarmony { Complementary, Analogous, Triadic, SplitComplementary, Tetradic, Monochrome }

/// <summary>
/// The game's color palette (LUNET-499): ONE canonical file per project (<see cref="DefaultPath"/>), used by the Sprite
/// and Tile Studios, exported to code (<c>Data/GamePalette.cs</c>) and read by the AI, so art, UI and code stay visually
/// consistent. Colors are ARGB, like sprites. Pure C#, no platform API.
/// </summary>
public sealed class ProjectPalette
{
    public const string DefaultPath = "Assets/palette.json";
    public const int MaxColors = 256;

    public int Version { get; set; } = 1;
    public string Name { get; set; } = "Paleta do jogo";
    public List<PaletteSwatch> Colors { get; set; } = [];

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never };

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static ProjectPalette Parse(string json)
    {
        var palette = JsonSerializer.Deserialize<ProjectPalette>(json) ?? throw new InvalidDataException("Paleta vazia.");
        if (palette.Version != 1) throw new InvalidDataException("Versão de paleta não suportada: " + palette.Version);
        palette.Colors ??= [];
        if (palette.Colors.Count > MaxColors) throw new InvalidDataException($"A paleta tem mais de {MaxColors} cores.");
        palette.Name = string.IsNullOrWhiteSpace(palette.Name) ? "Paleta do jogo" : palette.Name.Trim();
        palette.Colors = palette.Colors.Select((c, i) => new PaletteSwatch(string.IsNullOrWhiteSpace(c.Name) ? $"Cor{i + 1}" : c.Name.Trim(), c.Argb)).ToList();
        return palette;
    }

    public IReadOnlyList<uint> Argb => Colors.Select(c => c.Argb).ToArray();

    /// <summary>Adds colors not yet present (exact ARGB), naming them by hue; keeps the limit.</summary>
    public int AddRange(IEnumerable<uint> colors, string? prefix = null)
    {
        int added = 0;
        foreach (var color in colors)
        {
            if (Colors.Count >= MaxColors) break;
            if (Colors.Any(c => c.Argb == color)) continue;
            Colors.Add(new PaletteSwatch(UniqueName(prefix ?? HueName(color)), color));
            added++;
        }
        return added;
    }

    public string UniqueName(string baseName)
    {
        var clean = Identifier(baseName);
        if (!Colors.Any(c => Identifier(c.Name) == clean)) return clean;
        for (int i = 2; ; i++)
            if (!Colors.Any(c => Identifier(c.Name) == clean + i)) return clean + i;
    }

    // ---------- Generation ----------

    /// <summary>
    /// A pixel-art shade ramp: darker shades drift toward cool hues and lose saturation at the extremes, lighter ones drift
    /// toward warm hues (hue shifting), instead of plain black/white mixing that looks muddy.
    /// </summary>
    public static uint[] Ramp(uint baseColor, int steps, float hueShift = 20f)
    {
        steps = Math.Clamp(steps, 2, 16);
        var (h, s, v, a) = SpriteColor.ToHsv(baseColor);
        var result = new uint[steps];
        int middle = steps / 2;
        for (int i = 0; i < steps; i++)
        {
            float t = i < middle ? -(middle - i) / (float)middle
                : i > middle ? (i - middle) / (float)(steps - 1 - middle) : 0;
            t = Math.Clamp(t, -1, 1); // -1 darkest, 0 base, +1 lightest
            float hue = h + (t < 0 ? -t * hueShift * CoolDirection(h) : t * hueShift * WarmDirection(h));
            float value = t < 0 ? v * (1 + t * 0.75f) : v + (1 - v) * t * 0.9f;
            float saturation = t < 0 ? Math.Clamp(s * (1 + -t * 0.15f), 0, 1) : s * (1 - t * 0.6f);
            result[i] = i == middle ? baseColor : SpriteColor.FromHsv(hue, saturation, Math.Clamp(value, 0.04f, 1), a);
        }
        return result;
    }

    // Shadows go toward blue (240°), highlights toward yellow (60°), by the shortest way around the wheel.
    private static float CoolDirection(float hue) => Toward(hue, 240);
    private static float WarmDirection(float hue) => Toward(hue, 60);
    private static float Toward(float hue, float target)
    {
        var delta = ((target - hue) % 360 + 540) % 360 - 180;
        return Math.Abs(delta) < 1 ? 0 : Math.Sign(delta);
    }

    public static uint[] Harmony(uint baseColor, PaletteHarmony kind)
    {
        var (h, s, v, a) = SpriteColor.ToHsv(baseColor);
        uint At(float offset, float sat = -1, float val = -1) => SpriteColor.FromHsv(h + offset, sat < 0 ? s : sat, val < 0 ? v : val, a);
        return kind switch
        {
            PaletteHarmony.Complementary => [baseColor, At(180)],
            PaletteHarmony.Analogous => [At(-30), baseColor, At(30)],
            PaletteHarmony.Triadic => [baseColor, At(120), At(240)],
            PaletteHarmony.SplitComplementary => [baseColor, At(150), At(210)],
            PaletteHarmony.Tetradic => [baseColor, At(90), At(180), At(270)],
            _ => [At(0, s, v * 0.35f), At(0, s, v * 0.65f), baseColor, At(0, s * 0.6f, Math.Min(1, v + (1 - v) * 0.5f)), At(0, s * 0.25f, Math.Min(1, v + (1 - v) * 0.85f))],
        };
    }

    /// <summary>
    /// The <paramref name="max"/> most representative colors of an image: weighted k-means over the distinct opaque colors,
    /// seeded with the most frequent color and then the farthest ones, so small accents are not averaged away.
    /// </summary>
    public static uint[] FromPixels(IEnumerable<uint> pixels, int max)
    {
        var counts = pixels.Where(p => p >> 24 >= 128).GroupBy(p => p | 0xff000000).ToDictionary(g => g.Key, g => g.Count());
        if (counts.Count == 0) return [];
        max = Math.Clamp(max, 1, 64);
        var ordered = counts.OrderByDescending(c => c.Value).ThenBy(c => c.Key).ToArray();
        if (ordered.Length <= max) return ordered.Select(c => c.Key).ToArray();

        var centers = new List<(double R, double G, double B)> { Rgb(ordered[0].Key) };
        while (centers.Count < max)
        {
            var next = ordered.OrderByDescending(c => centers.Min(k => Distance(c.Key, Pack(k))) * Math.Sqrt(c.Value)).First().Key;
            centers.Add(Rgb(next));
        }
        var assignment = new int[ordered.Length];
        for (int iteration = 0; iteration < 12; iteration++)
        {
            bool moved = false;
            for (int i = 0; i < ordered.Length; i++)
            {
                int best = 0;
                double bestDistance = double.MaxValue;
                for (int k = 0; k < centers.Count; k++)
                {
                    var d = Distance(ordered[i].Key, Pack(centers[k]));
                    if (d < bestDistance) { bestDistance = d; best = k; }
                }
                if (assignment[i] != best || iteration == 0) { moved |= assignment[i] != best; assignment[i] = best; }
            }
            for (int k = 0; k < centers.Count; k++)
            {
                double r = 0, g = 0, b = 0, w = 0;
                for (int i = 0; i < ordered.Length; i++)
                {
                    if (assignment[i] != k) continue;
                    var (cr, cg, cb) = Rgb(ordered[i].Key);
                    r += cr * ordered[i].Value; g += cg * ordered[i].Value; b += cb * ordered[i].Value; w += ordered[i].Value;
                }
                if (w > 0) centers[k] = (r / w, g / w, b / w);
            }
            if (!moved && iteration > 0) break;
        }
        var weights = new double[centers.Count];
        for (int i = 0; i < ordered.Length; i++) weights[assignment[i]] += ordered[i].Value;
        return centers.Select((c, k) => (Color: Pack(c), Weight: weights[k])).Where(x => x.Weight > 0)
            .OrderByDescending(x => x.Weight).Select(x => x.Color).Distinct().ToArray();
    }

    private static (double R, double G, double B) Rgb(uint c) => (c >> 16 & 0xff, c >> 8 & 0xff, c & 0xff);
    private static uint Pack((double R, double G, double B) c) =>
        0xff000000u | (uint)Math.Round(Math.Clamp(c.R, 0, 255)) << 16 | (uint)Math.Round(Math.Clamp(c.G, 0, 255)) << 8 | (uint)Math.Round(Math.Clamp(c.B, 0, 255));

    /// <summary>Original starter palettes (generated, not copied from any product).</summary>
    public static IReadOnlyList<(string Id, string Name, uint[] Colors)> Presets { get; } =
    [
        ("noite", "Noite de lua", [0xff0b0e1a, 0xff1b2140, 0xff2f3a6b, 0xff4f62a8, 0xff8fa4e8, 0xffdfe6ff, 0xfff2d27a, 0xffb5762f]),
        ("floresta", "Floresta", [0xff10170f, 0xff1f3320, 0xff2f5a2c, 0xff4f8a3a, 0xff8cc152, 0xffd9e8a2, 0xff7a4b2a, 0xffc98a4b]),
        ("deserto", "Deserto", [0xff2a1a12, 0xff5a3420, 0xff9a5a2e, 0xffd28a46, 0xfff0c27a, 0xfffbe8c0, 0xff3d7d8c, 0xff86c5c9]),
        ("neon", "Neon", [0xff0a0614, 0xff221142, 0xff4a1a7a, 0xffff2e88, 0xffff9f1c, 0xfffff275, 0xff2ef2ff, 0xff7cff6b]),
        ("pastel", "Pastel", [0xff3a3550, 0xff6c6a8f, 0xffa8a5c8, 0xfff1eefa, 0xffffc4d6, 0xffffe3a8, 0xffbde8c8, 0xffa8d8f0]),
        ("mono", "Monocromática", [0xff0f0f12, 0xff2b2b33, 0xff4d4d59, 0xff77778a, 0xffa6a6b8, 0xffd6d6e0, 0xfff5f5fa]),
    ];

    public static ProjectPalette FromPreset(string id)
    {
        var preset = Presets.FirstOrDefault(p => p.Id == id);
        if (preset.Colors is null) throw new ArgumentException("Paleta inicial desconhecida: " + id);
        var palette = new ProjectPalette { Name = preset.Name };
        palette.AddRange(preset.Colors);
        return palette;
    }

    // ---------- Consistency ----------

    /// <summary>Perceptual-ish distance ("redmean"), good enough to pick the nearest palette color.</summary>
    public static double Distance(uint a, uint b)
    {
        int r1 = (int)(a >> 16 & 0xff), g1 = (int)(a >> 8 & 0xff), b1 = (int)(a & 0xff);
        int r2 = (int)(b >> 16 & 0xff), g2 = (int)(b >> 8 & 0xff), b2 = (int)(b & 0xff);
        double rm = (r1 + r2) / 2.0;
        int dr = r1 - r2, dg = g1 - g2, db = b1 - b2;
        return Math.Sqrt((2 + rm / 256) * dr * dr + 4 * dg * dg + (2 + (255 - rm) / 256) * db * db);
    }

    public uint Nearest(uint color)
    {
        if (Colors.Count == 0) return color;
        uint best = Colors[0].Argb;
        double bestDistance = double.MaxValue;
        foreach (var swatch in Colors)
        {
            var d = Distance(color, swatch.Argb);
            if (d < bestDistance) { bestDistance = d; best = swatch.Argb; }
        }
        return best & 0x00ffffff | color & 0xff000000; // keep the pixel's alpha
    }

    /// <summary>Snaps every visible pixel to the nearest palette color; transparent pixels stay untouched.</summary>
    public uint[] Remap(IReadOnlyList<uint> pixels)
    {
        var cache = new Dictionary<uint, uint>();
        var result = new uint[pixels.Count];
        for (int i = 0; i < pixels.Count; i++)
        {
            var p = pixels[i];
            if (p >> 24 == 0 || Colors.Count == 0) { result[i] = p; continue; }
            if (!cache.TryGetValue(p, out var mapped)) cache[p] = mapped = Nearest(p);
            result[i] = mapped;
        }
        return result;
    }

    /// <summary>How many distinct visible colors of <paramref name="pixels"/> are not in the palette (alpha ignored).</summary>
    public IReadOnlyList<uint> ColorsOutside(IEnumerable<uint> pixels)
    {
        var rgb = Colors.Select(c => c.Argb & 0xffffff).ToHashSet();
        return pixels.Where(p => p >> 24 != 0 && !rgb.Contains(p & 0xffffff)).Select(p => p | 0xff000000).Distinct().ToArray();
    }

    /// <summary>WCAG contrast ratio (1–21): text/UI legibility between two palette colors.</summary>
    public static double Contrast(uint a, uint b)
    {
        static double Lum(uint c)
        {
            static double Channel(uint v) { var s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
            return 0.2126 * Channel(c >> 16 & 0xff) + 0.7152 * Channel(c >> 8 & 0xff) + 0.0722 * Channel(c & 0xff);
        }
        double l1 = Lum(a), l2 = Lum(b);
        return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }

    // ---------- Export ----------

    /// <summary>A C# class with one <c>Color</c> per swatch, so code draws with the same colors as the art.</summary>
    /// <param name="namespace">Namespace of the game code, or null when the game uses the global namespace (the template default).</param>
    public string ToCSharp(string? @namespace, string className = "GamePalette")
    {
        var builder = new StringBuilder();
        builder.AppendLine("// Gerado pelo Palette Studio do Lunet a partir de " + DefaultPath + ". Edite a paleta e exporte de novo.");
        builder.AppendLine("using Lunet.Graphics;");
        builder.AppendLine();
        if (!string.IsNullOrWhiteSpace(@namespace))
        {
            var ns = string.Join('.', @namespace.Split('.').Select(Identifier));
            builder.Append("namespace ").Append(ns).AppendLine(";");
            builder.AppendLine();
        }
        builder.Append("/// <summary>").Append(System.Security.SecurityElement.Escape(Name)).AppendLine(": cores do jogo.</summary>");
        builder.Append("public static class ").AppendLine(Identifier(className));
        builder.AppendLine("{");
        var names = ExportNames();
        for (int i = 0; i < Colors.Count; i++)
        {
            uint c = Colors[i].Argb;
            builder.Append("    public static readonly Color ").Append(names[i]).Append(" = new(")
                .Append(c >> 16 & 0xff).Append(", ").Append(c >> 8 & 0xff).Append(", ").Append(c & 0xff).Append(", ").Append(c >> 24)
                .Append("); // ").AppendLine(SpriteColor.ToHex(c));
        }
        builder.AppendLine();
        builder.Append("    public static readonly Color[] All = [").Append(string.Join(", ", names)).AppendLine("];");
        builder.AppendLine("}");
        return builder.ToString();
    }

    /// <summary>Unique C# identifiers for the swatches, in order.</summary>
    public IReadOnlyList<string> ExportNames()
    {
        var used = new HashSet<string>(StringComparer.Ordinal) { "All" };
        var names = new List<string>();
        foreach (var swatch in Colors)
        {
            var name = Identifier(swatch.Name);
            for (int i = 2; !used.Add(name); i++) name = Identifier(swatch.Name) + i;
            names.Add(name);
        }
        return names;
    }

    /// <summary>GIMP/Krita/Aseprite palette (.gpl).</summary>
    public string ToGpl()
    {
        var builder = new StringBuilder();
        builder.AppendLine("GIMP Palette").Append("Name: ").AppendLine(Name).AppendLine("Columns: 8").AppendLine("#");
        foreach (var s in Colors)
            builder.AppendFormat(CultureInfo.InvariantCulture, "{0,3} {1,3} {2,3}\t{3}\n", s.Argb >> 16 & 0xff, s.Argb >> 8 & 0xff, s.Argb & 0xff, s.Name);
        return builder.ToString();
    }

    /// <summary>Reads .gpl or a plain list of hex colors (one per line, as many tools export).</summary>
    public static ProjectPalette Import(string text, string name)
    {
        var palette = new ProjectPalette { Name = name };
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') && SpriteColor.ParseHex(line) is null || line.StartsWith("GIMP", StringComparison.Ordinal) ||
                line.StartsWith("Name:", StringComparison.Ordinal) || line.StartsWith("Columns:", StringComparison.Ordinal)) continue;
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && byte.TryParse(parts[0], out var r) && byte.TryParse(parts[1], out var g) && byte.TryParse(parts[2], out var b))
            {
                var color = 0xff000000u | (uint)r << 16 | (uint)g << 8 | b;
                var label = parts.Length > 3 ? string.Join(' ', parts[3..]) : null;
                if (palette.Colors.All(c => c.Argb != color) && palette.Colors.Count < MaxColors)
                    palette.Colors.Add(new PaletteSwatch(palette.UniqueName(label ?? HueName(color)), color));
            }
            else if (SpriteColor.ParseHex(parts[0]) is { } hex) palette.AddRange([hex]);
        }
        return palette;
    }

    // ---------- Names ----------

    /// <summary>A Portuguese color-family name for a new swatch (Vermelho, Azul, Cinza…).</summary>
    public static string HueName(uint color)
    {
        var (h, s, v, _) = SpriteColor.ToHsv(color);
        if (v < 0.12f) return "Preto";
        if (s < 0.12f) return v > 0.9f ? "Branco" : "Cinza";
        string family = h switch
        {
            < 15 or >= 345 => "Vermelho", < 40 => "Laranja", < 65 => "Amarelo", < 160 => "Verde",
            < 195 => "Ciano", < 255 => "Azul", < 290 => "Roxo", _ => "Rosa",
        };
        if (family == "Laranja" && v < 0.6f) family = "Marrom";
        return family + (v < 0.4f ? "Escuro" : v > 0.85f && s < 0.5f ? "Claro" : "");
    }

    /// <summary>A valid C# identifier (PascalCase, ASCII), never empty, never starting with a digit.</summary>
    public static string Identifier(string text)
    {
        var decomposed = (text ?? "").Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        bool upper = true;
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (c < 128 && char.IsLetterOrDigit(c))
            {
                builder.Append(upper ? char.ToUpperInvariant(c) : c);
                upper = false;
            }
            else upper = true;
        }
        if (builder.Length == 0) builder.Append("Cor");
        if (char.IsDigit(builder[0])) builder.Insert(0, 'C');
        return builder.ToString();
    }
}
