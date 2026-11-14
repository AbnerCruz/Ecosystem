namespace Lunet.Studio;

/// <summary>
/// Conjunto de pixels selecionados de um frame (retângulo, laço ou varinha). Imutável depois de criado;
/// não conhece Android nem toque, para poder ser testado e reutilizado por outros hosts.
/// </summary>
public sealed class SpriteSelection
{
    private readonly bool[] _mask;

    private SpriteSelection(int width, int height, bool[] mask)
    {
        Width = width;
        Height = height;
        _mask = mask;
        int count = 0, minX = width, minY = height, maxX = -1, maxY = -1;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (!mask[y * width + x]) continue;
            count++;
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }
        Count = count;
        Bounds = count == 0 ? (0, 0, 0, 0) : (minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    public int Width { get; }
    public int Height { get; }
    public int Count { get; }
    public bool IsEmpty => Count == 0;
    public (int X, int Y, int Width, int Height) Bounds { get; }

    public bool Contains(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height && _mask[y * Width + x];

    /// <summary>Arestas entre pixels selecionados e não selecionados, em coordenadas de grade (x0,y0,x1,y1), para desenhar o contorno.</summary>
    public IReadOnlyList<(int X0, int Y0, int X1, int Y1)> OutlineSegments()
    {
        var edges = new List<(int, int, int, int)>();
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            if (!_mask[y * Width + x]) continue;
            if (!Contains(x, y - 1)) edges.Add((x, y, x + 1, y));
            if (!Contains(x, y + 1)) edges.Add((x, y + 1, x + 1, y + 1));
            if (!Contains(x - 1, y)) edges.Add((x, y, x, y + 1));
            if (!Contains(x + 1, y)) edges.Add((x + 1, y, x + 1, y + 1));
        }
        return edges;
    }

    /// <summary>Apaga (torna transparentes) os pixels selecionados do frame.</summary>
    public void Erase(SpriteDocument document, int frame)
    {
        if (document.Width != Width || document.Height != Height)
            throw new ArgumentException("A seleção pertence a outro tamanho de imagem.");
        var pixels = document.Frames[frame];
        for (int i = 0; i < pixels.Length; i++) if (_mask[i]) pixels[i] = 0;
    }

    /// <summary>Todos os pixels da imagem.</summary>
    public static SpriteSelection All(int width, int height) => new(width, height, Enumerable.Repeat(true, width * height).ToArray());

    /// <summary>Retângulo entre dois cantos inclusivos, recortado à imagem.</summary>
    public static SpriteSelection FromRectangle(int width, int height, int x0, int y0, int x1, int y1)
    {
        var mask = new bool[width * height];
        int left = Math.Max(0, Math.Min(x0, x1)), right = Math.Min(width - 1, Math.Max(x0, x1));
        int top = Math.Max(0, Math.Min(y0, y1)), bottom = Math.Min(height - 1, Math.Max(y0, y1));
        for (int y = top; y <= bottom; y++)
        for (int x = left; x <= right; x++) mask[y * width + x] = true;
        return new SpriteSelection(width, height, mask);
    }

    /// <summary>Laço: pixels cujo centro fica dentro do polígono (regra par-ímpar). Menos de 3 pontos não seleciona nada.</summary>
    public static SpriteSelection FromPolygon(int width, int height, IReadOnlyList<(int X, int Y)> points)
    {
        var mask = new bool[width * height];
        if (points.Count >= 3)
        {
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                mask[y * width + x] = Inside(points, x + .5f, y + .5f);
        }
        return new SpriteSelection(width, height, mask);
    }

    private static bool Inside(IReadOnlyList<(int X, int Y)> points, float px, float py)
    {
        bool inside = false;
        for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
        {
            float xi = points[i].X + .5f, yi = points[i].Y + .5f, xj = points[j].X + .5f, yj = points[j].Y + .5f;
            if ((yi > py) != (yj > py) && px < (xj - xi) * (py - yi) / (yj - yi) + xi) inside = !inside;
        }
        return inside;
    }

    /// <summary>Varinha mágica: região conectada (4 vizinhos) de cor parecida com a do pixel tocado. Tolerância 0–255 por canal.</summary>
    public static SpriteSelection FromColor(SpriteDocument document, int frame, int x, int y, int tolerance = 0)
    {
        int width = document.Width, height = document.Height;
        var mask = new bool[width * height];
        if ((uint)x >= (uint)width || (uint)y >= (uint)height) return new SpriteSelection(width, height, mask);
        var pixels = document.Frames[frame];
        uint target = pixels[y * width + x];
        tolerance = Math.Clamp(tolerance, 0, 255);
        var queue = new Queue<int>();
        queue.Enqueue(y * width + x);
        mask[y * width + x] = true;
        while (queue.TryDequeue(out int p))
        {
            void Visit(int n)
            {
                if (mask[n] || !Similar(pixels[n], target, tolerance)) return;
                mask[n] = true;
                queue.Enqueue(n);
            }
            if (p % width > 0) Visit(p - 1);
            if (p % width < width - 1) Visit(p + 1);
            if (p >= width) Visit(p - width);
            if (p < pixels.Length - width) Visit(p + width);
        }
        return new SpriteSelection(width, height, mask);
    }

    private static bool Similar(uint a, uint b, int tolerance)
    {
        if (a == b) return true;
        if (tolerance == 0) return false;
        for (int shift = 0; shift < 32; shift += 8)
            if (Math.Abs((int)((a >> shift) & 0xff) - (int)((b >> shift) & 0xff)) > tolerance) return false;
        return true;
    }
}

/// <summary>
/// Pixels levantados do frame, flutuando sobre ele até <see cref="Commit"/>. Mover, escalar e girar
/// mudam apenas esta camada; o frame original só é escrito ao confirmar, então cancelar é não fazer nada.
/// Reamostragem por vizinho mais próximo para preservar a estética de pixel art.
/// </summary>
public sealed class SpriteFloating
{
    private uint[] _base;
    private uint[] _pixels;

    private SpriteFloating(int docWidth, int docHeight, uint[] baseFrame, uint[] pixels, int width, int height, float cx, float cy)
    {
        DocumentWidth = docWidth;
        DocumentHeight = docHeight;
        _base = baseFrame;
        _pixels = pixels;
        Width = width;
        Height = height;
        CenterX = cx;
        CenterY = cy;
    }

    public int DocumentWidth { get; }
    public int DocumentHeight { get; }
    /// <summary>Tamanho da camada levantada, antes de escala e rotação.</summary>
    public int Width { get; private set; }
    public int Height { get; private set; }
    public float CenterX { get; private set; }
    public float CenterY { get; private set; }
    public float ScaleX { get; private set; } = 1;
    public float ScaleY { get; private set; } = 1;
    public float RotationDegrees { get; set; }

    /// <summary>Levanta os pixels selecionados. Com <paramref name="copy"/> o original permanece no lugar (duplicar).</summary>
    public static SpriteFloating Lift(SpriteDocument document, int frame, SpriteSelection selection, bool copy = false)
    {
        if (selection.IsEmpty) throw new ArgumentException("Nada selecionado.", nameof(selection));
        if (selection.Width != document.Width || selection.Height != document.Height)
            throw new ArgumentException("A seleção pertence a outro tamanho de imagem.", nameof(selection));
        var source = document.Frames[frame];
        var (bx, by, bw, bh) = selection.Bounds;
        var pixels = new uint[bw * bh];
        var baseFrame = (uint[])source.Clone();
        for (int y = 0; y < bh; y++)
        for (int x = 0; x < bw; x++)
        {
            if (!selection.Contains(bx + x, by + y)) continue;
            int index = (by + y) * document.Width + bx + x;
            pixels[y * bw + x] = source[index];
            if (!copy) baseFrame[index] = 0;
        }
        return new SpriteFloating(document.Width, document.Height, baseFrame, pixels, bw, bh, bx + bw / 2f, by + bh / 2f);
    }

    public void MoveBy(float dx, float dy) { CenterX += dx; CenterY += dy; }

    public void SetScale(float scaleX, float scaleY)
    {
        ScaleX = Math.Clamp(scaleX, 0.125f, 16f);
        ScaleY = Math.Clamp(scaleY, 0.125f, 16f);
    }

    public void FlipHorizontal()
    {
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width / 2; x++)
            (_pixels[y * Width + x], _pixels[y * Width + Width - 1 - x]) = (_pixels[y * Width + Width - 1 - x], _pixels[y * Width + x]);
    }

    public void FlipVertical()
    {
        for (int y = 0; y < Height / 2; y++)
        for (int x = 0; x < Width; x++)
            (_pixels[y * Width + x], _pixels[(Height - 1 - y) * Width + x]) = (_pixels[(Height - 1 - y) * Width + x], _pixels[y * Width + x]);
    }

    /// <summary>Gira os pixels 90° exatamente (sem reamostrar), mantendo o centro alinhado à grade.</summary>
    public void Rotate90(bool clockwise)
    {
        var rotated = new uint[_pixels.Length];
        int nw = Height, nh = Width;
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            int nx = clockwise ? Height - 1 - y : y;
            int ny = clockwise ? x : Width - 1 - x;
            rotated[ny * nw + nx] = _pixels[y * Width + x];
        }
        _pixels = rotated;
        Width = nw;
        Height = nh;
        (ScaleX, ScaleY) = (ScaleY, ScaleX);
        CenterX = MathF.Round(CenterX - nw / 2f) + nw / 2f;
        CenterY = MathF.Round(CenterY - nh / 2f) + nh / 2f;
    }

    /// <summary>Cantos da camada transformada (sup-esq, sup-dir, inf-dir, inf-esq) em coordenadas de pixel da imagem.</summary>
    public (float X, float Y)[] Corners()
    {
        float hw = Width * ScaleX / 2f, hh = Height * ScaleY / 2f;
        double rad = RotationDegrees * Math.PI / 180.0;
        float cos = (float)Math.Cos(rad), sin = (float)Math.Sin(rad);
        (float X, float Y) At(float lx, float ly) => (CenterX + lx * cos - ly * sin, CenterY + lx * sin + ly * cos);
        return [At(-hw, -hh), At(hw, -hh), At(hw, hh), At(-hw, hh)];
    }

    /// <summary>Frame completo com a camada aplicada. Não altera o documento.</summary>
    public uint[] Compose()
    {
        var result = (uint[])_base.Clone();
        var corners = Corners();
        int minX = Math.Max(0, (int)Math.Floor(corners.Min(c => c.X))), maxX = Math.Min(DocumentWidth - 1, (int)Math.Ceiling(corners.Max(c => c.X)));
        int minY = Math.Max(0, (int)Math.Floor(corners.Min(c => c.Y))), maxY = Math.Min(DocumentHeight - 1, (int)Math.Ceiling(corners.Max(c => c.Y)));
        double rad = RotationDegrees * Math.PI / 180.0;
        float cos = (float)Math.Cos(rad), sin = (float)Math.Sin(rad);
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            float dx = x + .5f - CenterX, dy = y + .5f - CenterY;
            float lx = (dx * cos + dy * sin) / ScaleX + Width / 2f;
            float ly = (-dx * sin + dy * cos) / ScaleY + Height / 2f;
            int u = (int)Math.Floor(lx), v = (int)Math.Floor(ly);
            if ((uint)u >= (uint)Width || (uint)v >= (uint)Height) continue;
            uint color = _pixels[v * Width + u];
            uint alpha = color >> 24;
            if (alpha == 0) continue;
            int index = y * DocumentWidth + x;
            result[index] = alpha == 255 ? color : Over(color, result[index]);
        }
        return result;
    }

    /// <summary>Grava a camada no frame e continua flutuando, deixando uma cópia para trás (carimbo).</summary>
    public void Stamp(SpriteDocument document, int frame)
    {
        Commit(document, frame);
        _base = (uint[])document.Frames[frame].Clone();
    }

    /// <summary>Escreve o resultado no frame do documento.</summary>
    public void Commit(SpriteDocument document, int frame)
    {
        if (document.Width != DocumentWidth || document.Height != DocumentHeight)
            throw new InvalidOperationException("O documento mudou de tamanho durante a seleção.");
        Array.Copy(Compose(), document.Frames[frame], DocumentWidth * DocumentHeight);
    }

    private static uint Over(uint top, uint bottom)
    {
        uint ta = top >> 24, ba = bottom >> 24;
        uint oa = ta + ba * (255 - ta) / 255;
        if (oa == 0) return 0;
        uint Channel(int shift)
        {
            uint t = (top >> shift) & 0xff, b = (bottom >> shift) & 0xff;
            return (t * ta * 255 + b * ba * (255 - ta)) / (oa * 255);
        }
        return oa << 24 | Channel(16) << 16 | Channel(8) << 8 | Channel(0);
    }
}
