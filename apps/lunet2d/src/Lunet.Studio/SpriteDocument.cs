using System.Text.Json;

namespace Lunet.Studio;

/// <summary>Inspectable sprite source: ARGB pixels, row-major frames, uniform FPS.</summary>
public sealed class SpriteDocument
{
    public int Version { get; set; } = 1;
    public int Width { get; set; } = 32;
    public int Height { get; set; } = 32;
    public int Fps { get; set; } = 8;
    public List<uint[]> Frames { get; set; } = [new uint[32 * 32]];

    public static SpriteDocument Create(int size) => new() { Width = size, Height = size, Frames = [new uint[CheckedSize(size, size)]] };
    private static int CheckedSize(int w, int h)
    {
        if (w is < 1 or > 128 || h is < 1 or > 128) throw new InvalidDataException("Dimensões devem ficar entre 1 e 128 pixels.");
        return w * h;
    }
    public void Validate()
    {
        int size = CheckedSize(Width, Height);
        if (Version != 1 || Fps is < 1 or > 60 || Frames is null || Frames.Count is < 1 or > 64 || Frames.Any(f => f is null || f.Length != size))
            throw new InvalidDataException("Sprite inválido: versão, FPS ou frames não suportados.");
    }
    public string ToJson() { Validate(); return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }); }
    public static SpriteDocument Parse(string json)
    {
        if (json.Length > 16_000_000) throw new InvalidDataException("Sprite excede 16 MB.");
        var doc = JsonSerializer.Deserialize<SpriteDocument>(json, new JsonSerializerOptions { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow }) ?? throw new InvalidDataException("Sprite vazio.");
        doc.Validate(); return doc;
    }
    public void AddFrame(int copyFrom = -1)
    {
        if (Frames.Count >= 64) throw new InvalidOperationException("Limite de 64 frames.");
        Frames.Add(copyFrom < 0 ? new uint[Width * Height] : (uint[])Frames[copyFrom].Clone());
    }
    public void RemoveFrame(int index) { if (Frames.Count > 1) Frames.RemoveAt(index); }
    public void Stroke(int frame, int x0, int y0, int x1, int y1, uint color)
    {
        // Bresenham fills gaps between touch samples, including fast stylus movement.
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1, dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1, error = dx + dy;
        while (true)
        {
            if ((uint)x0 < Width && (uint)y0 < Height) Frames[frame][y0 * Width + x0] = color;
            if (x0 == x1 && y0 == y1) break;
            int twice = 2 * error;
            if (twice >= dy) { error += dy; x0 += sx; }
            if (twice <= dx) { error += dx; y0 += sy; }
        }
    }
    public void Fill(int frame, int x, int y, uint color)
    {
        var pixels = Frames[frame]; uint old = pixels[y * Width + x];
        if (old == color) return;
        var queue = new Queue<int>(); queue.Enqueue(y * Width + x); pixels[y * Width + x] = color;
        while (queue.TryDequeue(out int p))
        {
            void Visit(int n) { if (pixels[n] == old) { pixels[n] = color; queue.Enqueue(n); } }
            if (p % Width > 0) Visit(p - 1);
            if (p % Width < Width - 1) Visit(p + 1);
            if (p >= Width) Visit(p - Width);
            if (p < pixels.Length - Width) Visit(p + Width);
        }
    }
    public void FlipHorizontal(int frame)
    {
        var pixels = Frames[frame];
        for (int y = 0; y < Height; y++) for (int x = 0; x < Width / 2; x++)
        { int a = y * Width + x, b = y * Width + Width - x - 1; (pixels[a], pixels[b]) = (pixels[b], pixels[a]); }
    }
    /// <summary>RGBA bytes for a horizontal sheet, directly usable by SpriteSheet.</summary>
    public byte[] SheetRgba()
    {
        Validate(); var data = new byte[Width * Frames.Count * Height * 4]; int i = 0;
        for (int y = 0; y < Height; y++) foreach (var frame in Frames) for (int x = 0; x < Width; x++)
        { uint c = frame[y * Width + x]; data[i++] = (byte)(c >> 16); data[i++] = (byte)(c >> 8); data[i++] = (byte)c; data[i++] = (byte)(c >> 24); }
        return data;
    }
}
