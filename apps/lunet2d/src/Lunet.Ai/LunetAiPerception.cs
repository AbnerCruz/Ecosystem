using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentRuntime;
using Lunet.Core;

namespace Lunet.Ai;

/// <summary>Decoded RGBA image (8 bits per channel, row-major).</summary>
public sealed record LunetAiImage(int Width, int Height, byte[] Rgba);

/// <summary>Host image decoding (Android: BitmapFactory for PNG/JPG/WebP). Returns null when the file is not an image.</summary>
public interface ILunetAiImages
{
    LunetAiImage? Load(string fullPath, string relativePath);
}

/// <summary>
/// LUNET-489: turns any picture (hand drawing on paper, photo, PNG) into a small pixel grid with a color legend,
/// so a text-only model can "see" it and redraw it as a sprite. Paper/background is removed; colors are clustered
/// or snapped to the project's palette for artistic coherence.
/// </summary>
public sealed class LunetAiImageGrid
{
    private const string Symbols = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public int Width { get; }
    public int Height { get; }
    public int SourceWidth { get; }
    public int SourceHeight { get; }
    public uint? Background { get; }
    public IReadOnlyList<string> Rows { get; }
    public IReadOnlyDictionary<char, uint> Palette { get; }

    private LunetAiImageGrid(int width, int height, int sw, int sh, uint? background, string[] rows, Dictionary<char, uint> palette)
    { Width = width; Height = height; SourceWidth = sw; SourceHeight = sh; Background = background; Rows = rows; Palette = palette; }

    public static LunetAiImageGrid From(LunetAiImage image, int maxSide = 48, int maxColors = 12, IReadOnlyList<uint>? snapPalette = null)
    {
        maxSide = Math.Clamp(maxSide, 4, 64);
        maxColors = Math.Clamp(maxColors, 2, 32);
        int w = image.Width, h = image.Height;
        var rgba = image.Rgba;
        uint Pixel(int x, int y) { int i = (y * w + x) * 4; return (uint)rgba[i + 3] << 24 | (uint)rgba[i] << 16 | (uint)rgba[i + 1] << 8 | rgba[i + 2]; }

        // Background = dominant color on the border (paper, sky, flat fill), unless the border is transparent.
        var border = new Dictionary<uint, int>();
        void Count(int x, int y)
        {
            var p = Pixel(x, y);
            var key = p >> 24 < 32 ? 0u : 0xff000000u | (p >> 16 & 0xf0) << 16 | (p >> 8 & 0xf0) << 8 | (p & 0xf0);
            border[key] = border.GetValueOrDefault(key) + 1;
        }
        for (int x = 0; x < w; x++) { Count(x, 0); Count(x, h - 1); }
        for (int y = 0; y < h; y++) { Count(0, y); Count(w - 1, y); }
        var dominant = border.OrderByDescending(b => b.Value).First();
        bool hasBackground = dominant.Value >= (2 * (w + h)) * 0.35;
        uint? background = null;
        if (hasBackground && dominant.Key != 0)
        {
            // Average real color of the border pixels in that bucket.
            long r = 0, g = 0, b = 0, n = 0;
            for (int x = 0; x < w; x++) foreach (int y in new[] { 0, h - 1 }) Acc(x, y);
            for (int y = 0; y < h; y++) foreach (int x in new[] { 0, w - 1 }) Acc(x, y);
            void Acc(int x, int y)
            {
                var p = Pixel(x, y);
                if ((0xff000000u | (p >> 16 & 0xf0) << 16 | (p >> 8 & 0xf0) << 8 | (p & 0xf0)) != dominant.Key) return;
                r += p >> 16 & 0xff; g += p >> 8 & 0xff; b += p & 0xff; n++;
            }
            background = 0xff000000u | (uint)(r / n) << 16 | (uint)(g / n) << 8 | (uint)(b / n);
        }
        bool IsBackground(uint p) => p >> 24 < 32 || (background is { } bg && Distance(p, bg) < 48);

        double scale = Math.Max(1.0, Math.Max(w, h) / (double)maxSide);
        int gw = Math.Max(1, (int)Math.Round(w / scale)), gh = Math.Max(1, (int)Math.Round(h / scale));
        var cells = new uint?[gw * gh];
        for (int cy = 0; cy < gh; cy++)
            for (int cx = 0; cx < gw; cx++)
            {
                int x0 = (int)(cx * (double)w / gw), x1 = Math.Max(x0 + 1, (int)((cx + 1) * (double)w / gw));
                int y0 = (int)(cy * (double)h / gh), y1 = Math.Max(y0 + 1, (int)((cy + 1) * (double)h / gh));
                long r = 0, g = 0, b = 0, a = 0, fg = 0, total = 0;
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++)
                    {
                        total++;
                        var p = Pixel(x, y);
                        if (IsBackground(p)) continue;
                        fg++; r += p >> 16 & 0xff; g += p >> 8 & 0xff; b += p & 0xff; a += p >> 24;
                    }
                if (fg >= Math.Max(1, total * 0.18))
                    cells[cy * gw + cx] = (uint)(a / fg) << 24 | (uint)(r / fg) << 16 | (uint)(g / fg) << 8 | (uint)(b / fg);
            }

        var colors = cells.Where(c => c is not null).Select(c => c!.Value).ToList();
        uint[] centers = snapPalette is { Count: > 0 }
            ? snapPalette.Where(c => c >> 24 != 0).Distinct().ToArray()
            : Cluster(colors, maxColors);
        var used = new Dictionary<uint, char>();
        var palette = new Dictionary<char, uint> { ['.'] = 0 };
        var rows = new string[gh];
        for (int cy = 0; cy < gh; cy++)
        {
            var row = new StringBuilder(gw);
            for (int cx = 0; cx < gw; cx++)
            {
                if (cells[cy * gw + cx] is not { } c || centers.Length == 0) { row.Append('.'); continue; }
                var nearest = centers.MinBy(center => Distance(center, c));
                if (!used.TryGetValue(nearest, out var symbol))
                {
                    symbol = Symbols[Math.Min(used.Count, Symbols.Length - 1)];
                    used[nearest] = symbol;
                    palette[symbol] = 0xff000000u | (nearest & 0xffffff);
                }
                row.Append(symbol);
            }
            rows[cy] = row.ToString();
        }
        return new LunetAiImageGrid(gw, gh, w, h, background, rows, palette);
    }

    public string Describe()
    {
        var text = new StringBuilder();
        text.AppendLine($"Imagem {SourceWidth}×{SourceHeight} px → grade {Width}×{Height} (cada célula ≈ {SourceWidth / (double)Width:0.#} px).");
        text.AppendLine(Background is { } bg ? $"Fundo detectado e removido: {Hex(bg)} (vira '.')." : "Sem fundo uniforme; '.' = transparente.");
        text.AppendLine("Legenda: " + string.Join(", ", Palette.Where(p => p.Key != '.').Select(p => $"{p.Key}={Hex(p.Value)}")) + ", .=transparente");
        text.AppendLine("Grade (linha 0 no topo):");
        foreach (var row in Rows) text.AppendLine(row);
        return text.ToString();
    }

    internal static string Hex(uint c) => (c >> 24) == 0xff ? $"#{c & 0xffffff:X6}" : $"#{c:X8}";

    internal static double Distance(uint a, uint b)
    {
        int dr = (int)(a >> 16 & 0xff) - (int)(b >> 16 & 0xff);
        int dg = (int)(a >> 8 & 0xff) - (int)(b >> 8 & 0xff);
        int db = (int)(a & 0xff) - (int)(b & 0xff);
        // Perceptual-ish weights (green matters most).
        return Math.Sqrt(2 * dr * dr + 4 * dg * dg + 3 * db * db) / 3;
    }

    /// <summary>Deterministic k-means with farthest-point initialization; merges near-duplicate centers.</summary>
    private static uint[] Cluster(List<uint> colors, int k)
    {
        if (colors.Count == 0) return [];
        var distinct = colors.Distinct().ToList();
        var centers = new List<uint> { colors.GroupBy(c => c).OrderByDescending(g => g.Count()).First().Key };
        while (centers.Count < Math.Min(k, distinct.Count))
        {
            var far = distinct.MaxBy(c => centers.Min(center => Distance(center, c)));
            if (centers.Min(center => Distance(center, far)) < 20) break;
            centers.Add(far);
        }
        for (int iteration = 0; iteration < 8; iteration++)
        {
            var sums = new long[centers.Count, 4];
            foreach (var c in colors)
            {
                int best = 0;
                for (int i = 1; i < centers.Count; i++) if (Distance(centers[i], c) < Distance(centers[best], c)) best = i;
                sums[best, 0] += c >> 16 & 0xff; sums[best, 1] += c >> 8 & 0xff; sums[best, 2] += c & 0xff; sums[best, 3]++;
            }
            for (int i = 0; i < centers.Count; i++)
                if (sums[i, 3] > 0)
                    centers[i] = 0xff000000u | (uint)(sums[i, 0] / sums[i, 3]) << 16 | (uint)(sums[i, 1] / sums[i, 3]) << 8 | (uint)(sums[i, 2] / sums[i, 3]);
        }
        var merged = new List<uint>();
        foreach (var c in centers) if (merged.All(m => Distance(m, c) >= 16)) merged.Add(c);
        return merged.ToArray();
    }
}

/// <summary><c>lunet.image.inspect</c>: look at an image of the project (attachments live in Notes/).</summary>
public sealed class LunetAiImageTool : ITool
{
    public const string Name = "lunet.image.inspect";
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif" };
    private readonly LunetProject _project;
    private readonly ILunetAiImages _images;
    public ToolDescriptor Descriptor { get; }

    public LunetAiImageTool(LunetProject project, ILunetAiImages images)
    {
        _project = project;
        _images = images;
        Descriptor = new ToolDescriptor(Name, "1.0.0",
            "Look at an image file of the project (png/jpg/webp; attachments and sketches are in Notes/). Returns a pixel grid " +
            "(max_side cells, background removed) with a color legend, ready to be redrawn with lunet.assets.sprite. " +
            "palette='project' snaps colors to the project's existing palette for artistic coherence.",
            """{"type":"object","properties":{"path":{"type":"string"},"max_side":{"type":"integer","minimum":4,"maximum":64},"max_colors":{"type":"integer","minimum":2,"maximum":32},"palette":{"type":"string","enum":["auto","project"]}},"required":["path"],"additionalProperties":false}""",
            new HashSet<string> { Permissions.AgentAct, Permissions.FsRead }, RiskClass.Read,
            ContextPath.Create(
                new ContextStep(ContextLevel.Ecosystem, "ecosystem"),
                new ContextStep(ContextLevel.Product, "lunet2d"),
                new ContextStep(ContextLevel.Project, project.Manifest.GameId)), true);
    }

    public ValueTask<ToolOutcome> InvokeAsync(ToolInvocation invocation, ToolContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var args = invocation.Arguments;
        string Arg(string key) => args.ValueKind == JsonValueKind.Object && args.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        int Int(string key, int fallback) => args.ValueKind == JsonValueKind.Object && args.TryGetProperty(key, out var v) && v.TryGetInt32(out var i) ? i : fallback;
        var relative = Arg("path").Replace('\\', '/').Trim().TrimStart('/');
        var root = Path.GetFullPath(_project.Directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(root, StringComparison.Ordinal) || !Extensions.Contains(Path.GetExtension(full)))
            return ValueTask.FromResult(ToolOutcome.Error("Use o caminho de uma imagem do projeto (png, jpg, webp…), por exemplo Notes/desenho.jpg."));
        if (!File.Exists(full)) return ValueTask.FromResult(ToolOutcome.Error("Imagem não encontrada: " + relative));
        LunetAiImage? image;
        try { image = _images.Load(full, relative); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { image = null; }
        if (image is null) return ValueTask.FromResult(ToolOutcome.Error("Não foi possível decodificar a imagem."));
        var snap = Arg("palette") == "project" ? LunetAiProjectStyle.Analyze(_project).Palette : null;
        var grid = LunetAiImageGrid.From(image, Int("max_side", 48), Int("max_colors", 12), snap);
        return ValueTask.FromResult(ToolOutcome.Ok(relative + "\n" + grid.Describe()));
    }
}

/// <summary>The project's established art direction and conventions, so everything the AI adds looks and reads alike.</summary>
public sealed class LunetAiProjectStyle
{
    public IReadOnlyList<uint> Palette { get; init; } = [];
    /// <summary>The official palette of the game (Assets/palette.json, LUNET-499); when present it IS <see cref="Palette"/>.</summary>
    public IReadOnlyList<(string Name, uint Argb)> OfficialPalette { get; init; } = [];
    public bool PaletteInCode { get; init; }
    public const string PaletteFile = "Assets/palette.json";
    public (int Width, int Height)? CommonSpriteSize { get; init; }
    public IReadOnlyList<string> SpriteNames { get; init; } = [];
    public IReadOnlyList<string> OtherImages { get; init; } = [];
    public IReadOnlyList<string> Classes { get; init; } = [];
    public IReadOnlyList<string> Namespaces { get; init; } = [];
    public string Orientation { get; init; } = "portrait";
    public string StyleNotes { get; init; } = "";

    private static readonly Regex StyleNoteName = new("estilo|style|arte|visual|dire[cç][aã]o|paleta|art", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ClassDeclaration = new(@"\b(?:class|record|struct)\s+([A-Z]\w*)", RegexOptions.CultureInvariant);
    private static readonly Regex UsingDeclaration = new(@"^\s*using\s+(Lunet[\w.]*)\s*;", RegexOptions.Multiline | RegexOptions.CultureInvariant);

    public static LunetAiProjectStyle Analyze(LunetProject project)
    {
        var files = project.ListFiles();
        var colorCounts = new Dictionary<uint, long>();
        var sizes = new Dictionary<(int, int), int>();
        var sprites = new List<string>();
        foreach (var path in files.Where(f => f.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase)).Take(60))
        {
            sprites.Add(path[..^".sprite.json".Length]);
            try
            {
                var full = Path.Combine(project.Directory, path);
                if (new FileInfo(full).Length > 16_000_000) continue;
                using var json = JsonDocument.Parse(File.ReadAllText(full));
                int w = json.RootElement.GetProperty("Width").GetInt32(), h = json.RootElement.GetProperty("Height").GetInt32();
                sizes[(w, h)] = sizes.GetValueOrDefault((w, h)) + 1;
                foreach (var frame in json.RootElement.GetProperty("Frames").EnumerateArray())
                    foreach (var pixel in frame.EnumerateArray())
                    {
                        var c = pixel.GetUInt32();
                        if (c >> 24 >= 32) colorCounts[c] = colorCounts.GetValueOrDefault(c) + 1;
                    }
            }
            catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { }
        }
        var classes = new SortedSet<string>(StringComparer.Ordinal);
        var namespaces = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var source in project.LoadSources().Take(80))
        {
            foreach (Match m in ClassDeclaration.Matches(source.Text)) classes.Add(m.Groups[1].Value);
            foreach (Match m in UsingDeclaration.Matches(source.Text)) namespaces.Add(m.Groups[1].Value);
        }
        var notes = new StringBuilder();
        foreach (var path in files.Where(f => f.StartsWith(ProjectNotes.Folder + "/", StringComparison.Ordinal) && f.EndsWith(".md", StringComparison.OrdinalIgnoreCase)))
        {
            string content;
            try { content = project.ReadText(path); } catch (IOException) { continue; }
            if (!StyleNoteName.IsMatch(Path.GetFileName(path)) && !StyleNoteName.IsMatch(ProjectNotes.TitleOf(path, content))) continue;
            notes.AppendLine($"[{path}]").AppendLine(content.Length > 3000 ? content[..3000] + "…" : content);
            if (notes.Length > 6000) break;
        }
        var sheetNames = sprites.Select(s => s + ".png").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var official = ReadOfficialPalette(project);
        return new LunetAiProjectStyle
        {
            Palette = official.Count > 0 ? official.Select(c => c.Argb).ToArray()
                : colorCounts.OrderByDescending(c => c.Value).ThenBy(c => c.Key).Take(32).Select(c => c.Key).ToArray(),
            OfficialPalette = official,
            PaletteInCode = files.Contains("Data/GamePalette.cs"),
            CommonSpriteSize = sizes.Count == 0 ? null : sizes.OrderByDescending(s => s.Value).First().Key,
            SpriteNames = sprites,
            OtherImages = files.Where(f => (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                                           && !sheetNames.Contains(f)).Take(40).ToArray(),
            Classes = classes.Take(60).ToArray(),
            Namespaces = namespaces.ToArray(),
            Orientation = project.Manifest.Orientation,
            StyleNotes = notes.ToString(),
        };
    }

    private static IReadOnlyList<(string Name, uint Argb)> ReadOfficialPalette(LunetProject project)
    {
        try
        {
            var full = Path.Combine(project.Directory, PaletteFile);
            if (!File.Exists(full) || new FileInfo(full).Length > 1_000_000) return [];
            using var json = JsonDocument.Parse(File.ReadAllText(full));
            return json.RootElement.GetProperty("Colors").EnumerateArray()
                .Select(c => (c.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "", c.GetProperty("Argb").GetUInt32()))
                .Take(256).ToArray();
        }
        catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { return []; }
    }

    public string Describe()
    {
        var text = new StringBuilder();
        text.AppendLine($"Orientação do jogo: {Orientation}.");
        if (OfficialPalette.Count > 0)
            text.AppendLine($"PALETA OFICIAL do jogo ({PaletteFile}) — use somente estas cores em sprites, tiles, UI e código: " +
                            string.Join(", ", OfficialPalette.Select(c => $"{c.Name}={LunetAiImageGrid.Hex(c.Argb)}")) +
                            (PaletteInCode ? ". No código, use GamePalette.<Nome> (Data/GamePalette.cs) em vez de new Color(...)." : "."));
        else text.AppendLine(Palette.Count == 0
            ? "Paleta do projeto: ainda não há sprites nem paleta oficial; escolha uma paleta pequena e coerente e grave-a em " + PaletteFile + " ({\"Version\":1,\"Name\":\"…\",\"Colors\":[{\"Name\":\"Ceu\",\"Argb\":4283392680}]}, ARGB decimal)."
            : "Paleta do projeto (mais usadas primeiro): " + string.Join(" ", Palette.Select(LunetAiImageGrid.Hex)));
        text.AppendLine(CommonSpriteSize is { } size
            ? $"Tamanho de sprite predominante: {size.Width}×{size.Height}."
            : "Tamanho de sprite predominante: nenhum ainda.");
        if (SpriteNames.Count > 0) text.AppendLine("Sprites existentes: " + string.Join(", ", SpriteNames));
        if (OtherImages.Count > 0) text.AppendLine("Outras imagens: " + string.Join(", ", OtherImages));
        if (Classes.Count > 0) text.AppendLine("Classes do código: " + string.Join(", ", Classes));
        if (Namespaces.Count > 0) text.AppendLine("APIs do Lunet em uso: " + string.Join(", ", Namespaces));
        text.AppendLine(StyleNotes.Length > 0
            ? "Notas de estilo da pessoa (siga-as):\n" + StyleNotes
            : "Sem nota de estilo: proponha uma (Notes/estilo-visual.md) quando definir algo novo de arte.");
        return text.ToString();
    }
}

/// <summary><c>lunet.project.style</c>: read the art direction and conventions before producing anything.</summary>
public sealed class LunetAiStyleTool : ITool
{
    public const string Name = "lunet.project.style";
    private readonly LunetProject _project;
    public ToolDescriptor Descriptor { get; }

    public LunetAiStyleTool(LunetProject project)
    {
        _project = project;
        Descriptor = new ToolDescriptor(Name, "1.0.0",
            "Read the project's established art direction and conventions: palette in use, predominant sprite size, existing " +
            "sprites and images, classes and Lunet APIs used, orientation and the person's style notes. Call before creating art or new systems.",
            """{"type":"object","properties":{},"additionalProperties":false}""",
            new HashSet<string> { Permissions.AgentAct, Permissions.FsRead }, RiskClass.Read,
            ContextPath.Create(
                new ContextStep(ContextLevel.Ecosystem, "ecosystem"),
                new ContextStep(ContextLevel.Product, "lunet2d"),
                new ContextStep(ContextLevel.Project, project.Manifest.GameId)), true);
    }

    public ValueTask<ToolOutcome> InvokeAsync(ToolInvocation invocation, ToolContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ToolOutcome.Ok(LunetAiProjectStyle.Analyze(_project).Describe()));
    }
}
