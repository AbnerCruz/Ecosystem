using System.Globalization;
using System.Text.Json;
using AgentRuntime;
using Lunet.Core;

namespace Lunet.Ai;

/// <summary>
/// <c>lunet.assets.sprite</c> (LUNET-487): the agent draws pixel art as character rows plus a palette and gets the
/// same files the Sprite Studio produces — an editable <c>.sprite.json</c> source and the PNG sheet the game loads.
/// </summary>
public sealed class LunetAiSpriteTool : ITool
{
    public const string Name = "lunet.assets.sprite";
    private const int MaxSize = 128, MaxFrames = 64;
    private readonly LunetProject _project;
    private readonly LunetAiCheckpoint _checkpoint;
    public ToolDescriptor Descriptor { get; }

    public LunetAiSpriteTool(LunetProject project, LunetAiCheckpoint checkpoint)
    {
        _project = project;
        _checkpoint = checkpoint;
        Descriptor = new ToolDescriptor(Name, "1.0.0",
            "Create or replace a pixel-art sprite/animation. 'path' is under Assets/ or Content/ without extension; " +
            "'palette' maps single characters to colors (#RRGGBB, #AARRGGBB or 'transparent'); 'frames' is a list of frames, " +
            "each a list of equal-length rows using palette characters (max 128x128, 64 frames). Writes <path>.sprite.json " +
            "(editable in the Sprite Studio) and <path>.png (horizontal sheet, load with Content.LoadTexture).",
            """{"type":"object","properties":{"path":{"type":"string"},"fps":{"type":"integer","minimum":1,"maximum":60},"palette":{"type":"object","additionalProperties":{"type":"string"}},"frames":{"type":"array","items":{"type":"array","items":{"type":"string"}}}},"required":["path","palette","frames"],"additionalProperties":false}""",
            new HashSet<string> { Permissions.AgentAct, Permissions.FsWrite }, RiskClass.Write,
            ContextPath.Create(
                new ContextStep(ContextLevel.Ecosystem, "ecosystem"),
                new ContextStep(ContextLevel.Product, "lunet2d"),
                new ContextStep(ContextLevel.Project, project.Manifest.GameId)), false);
    }

    public ValueTask<ToolOutcome> InvokeAsync(ToolInvocation invocation, ToolContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var (path, fps, palette, frames) = Parse(invocation.Arguments);
            int height = frames[0].Length, width = frames[0][0].Length;
            var pixels = frames.Select(frame =>
            {
                var data = new uint[width * height];
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        data[y * width + x] = palette[frame[y][x]];
                return data;
            }).ToList();

            var json = JsonSerializer.Serialize(new { Version = 1, Width = width, Height = height, Fps = fps, Frames = pixels },
                new JsonSerializerOptions { WriteIndented = true });
            var sheet = new byte[width * pixels.Count * height * 4];
            int i = 0;
            for (int y = 0; y < height; y++)
                foreach (var frame in pixels)
                    for (int x = 0; x < width; x++)
                    {
                        uint c = frame[y * width + x];
                        sheet[i++] = (byte)(c >> 16); sheet[i++] = (byte)(c >> 8); sheet[i++] = (byte)c; sheet[i++] = (byte)(c >> 24);
                    }
            var png = PngWriter.Encode(width * pixels.Count, height, sheet);

            _checkpoint.Capture(path + ".sprite.json");
            _checkpoint.Capture(path + ".png");
            _project.WriteText(path + ".sprite.json", json);
            using (var stream = new MemoryStream(png))
                _project.Import(System.IO.Path.GetDirectoryName(path)!.Replace('\\', '/'), System.IO.Path.GetFileName(path) + ".png", stream);

            var frameList = string.Join(", ", Enumerable.Range(0, pixels.Count));
            return ValueTask.FromResult(ToolOutcome.Ok(JsonSerializer.Serialize(new
            {
                source = path + ".sprite.json",
                sheet = path + ".png",
                width, height, frames = pixels.Count, fps,
                usage = $"var sheet = new SpriteSheet(Content.LoadTexture(\"{path}.png\"), {width}, {height}); " +
                        $"var clip = SpriteAnimationClip.FromSheet(sheet, new[] {{ {frameList} }}, 1.0 / {fps}); var animator = new SpriteAnimator(clip);",
            }), new ArtifactRef(path, "lunet.asset", path + ".png")));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or ProjectException
                                   or KeyNotFoundException or JsonException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult(ToolOutcome.Error(ex.Message));
        }
    }

    private static (string Path, int Fps, Dictionary<char, uint> Palette, string[][] Frames) Parse(JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.Object) throw new ArgumentException("Entrada inválida.");
        var path = input.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String ? (p.GetString() ?? "") : "";
        path = path.Replace('\\', '/').Trim().Trim('/');
        if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) path = path[..^4];
        if (path.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase)) path = path[..^12];
        if (!(path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Content/", StringComparison.Ordinal))
            || path.Split('/').Any(s => s.Length == 0 || s == "." || s == ".." || s.StartsWith('.'))
            || path.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '/' or '_' or '-')))
            throw new ArgumentException("Use um caminho em Assets/ ou Content/ com letras, números, _ ou -, sem extensão.");
        int fps = input.TryGetProperty("fps", out var f) && f.TryGetInt32(out var parsedFps) ? Math.Clamp(parsedFps, 1, 60) : 8;

        var palette = new Dictionary<char, uint>();
        if (!input.TryGetProperty("palette", out var pal) || pal.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Informe a paleta.");
        foreach (var entry in pal.EnumerateObject())
        {
            if (entry.Name.Length != 1) throw new ArgumentException("Cada chave da paleta deve ser um único caractere.");
            var value = (entry.Value.GetString() ?? "").Trim();
            palette[entry.Name[0]] = value.Equals("transparent", StringComparison.OrdinalIgnoreCase) ? 0u : ParseColor(value);
        }
        if (!input.TryGetProperty("frames", out var fr) || fr.ValueKind != JsonValueKind.Array || fr.GetArrayLength() is 0 or > MaxFrames)
            throw new ArgumentException($"Informe de 1 a {MaxFrames} frames.");
        var frames = fr.EnumerateArray().Select(frame => frame.ValueKind == JsonValueKind.Array
            ? frame.EnumerateArray().Select(row => row.GetString() ?? "").ToArray()
            : throw new ArgumentException("Cada frame é uma lista de linhas.")).ToArray();
        int height = frames[0].Length, width = height == 0 ? 0 : frames[0][0].Length;
        if (width is < 1 or > MaxSize || height is < 1 or > MaxSize)
            throw new ArgumentException($"O sprite deve ter de 1 a {MaxSize} pixels de lado.");
        foreach (var frame in frames)
        {
            if (frame.Length != height || frame.Any(row => row.Length != width))
                throw new ArgumentException($"Todos os frames precisam ter {height} linhas de {width} caracteres.");
            foreach (var c in frame.SelectMany(row => row))
                if (!palette.ContainsKey(c)) throw new ArgumentException($"O caractere '{c}' não está na paleta.");
        }
        return (path, fps, palette, frames);
    }

    private static uint ParseColor(string value)
    {
        var hex = value.TrimStart('#');
        if (hex.Length == 3) hex = string.Concat(hex.Select(c => new string(c, 2)));
        if (hex.Length is not (6 or 8) || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var color))
            throw new ArgumentException($"Cor inválida: {value}");
        return hex.Length == 6 ? 0xff000000u | color : color;
    }
}
