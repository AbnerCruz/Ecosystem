using System.Numerics;
using System.Text;

namespace Lunet.Graphics;

/// <summary>
/// Fonte bitmap embutida (5×7 pixels, com acentos do português). Serve para HUD e depuração;
/// SpriteFont.FromBitmap aceita fontes personalizadas; TrueType permanece separado.
/// </summary>
/// <example>
/// <code>
/// var title = SpriteFont.CreateDefault(device);
/// Vector2 size = title.Measure("Olá", 2f);
/// batch.DrawString(title, "Olá", (device.ViewSize - size) / 2, Color.White, 2f);
/// </code>
/// </example>
public sealed class SpriteFont
{
    private const int CellWidth = 6;
    private const int CellHeight = 10; // 2 linhas para acentos de maiúsculas + 8 do glifo
    private const int Columns = 16;

    private static readonly (string Char, string Rows)[] Base =
    [
        (" ", "00000,00000,00000,00000,00000,00000,00000,00000"),
        ("!", "00100,00100,00100,00100,00100,00000,00100,00000"),
        ("\"", "01010,01010,01010,00000,00000,00000,00000,00000"),
        ("#", "01010,01010,11111,01010,11111,01010,01010,00000"),
        ("$", "00100,01111,10100,01110,00101,11110,00100,00000"),
        ("%", "11000,11001,00010,00100,01000,10011,00011,00000"),
        ("&", "01100,10010,10100,01000,10101,10010,01101,00000"),
        ("'", "01100,00100,01000,00000,00000,00000,00000,00000"),
        ("(", "00010,00100,01000,01000,01000,00100,00010,00000"),
        (")", "01000,00100,00010,00010,00010,00100,01000,00000"),
        ("*", "00000,00100,10101,01110,10101,00100,00000,00000"),
        ("+", "00000,00100,00100,11111,00100,00100,00000,00000"),
        (",", "00000,00000,00000,00000,01100,00100,01000,00000"),
        ("-", "00000,00000,00000,11111,00000,00000,00000,00000"),
        (".", "00000,00000,00000,00000,00000,01100,01100,00000"),
        ("/", "00000,00001,00010,00100,01000,10000,00000,00000"),
        ("0", "01110,10001,10011,10101,11001,10001,01110,00000"),
        ("1", "00100,01100,00100,00100,00100,00100,01110,00000"),
        ("2", "01110,10001,00001,00010,00100,01000,11111,00000"),
        ("3", "11111,00010,00100,00010,00001,10001,01110,00000"),
        ("4", "00010,00110,01010,10010,11111,00010,00010,00000"),
        ("5", "11111,10000,11110,00001,00001,10001,01110,00000"),
        ("6", "00110,01000,10000,11110,10001,10001,01110,00000"),
        ("7", "11111,00001,00010,00100,01000,01000,01000,00000"),
        ("8", "01110,10001,10001,01110,10001,10001,01110,00000"),
        ("9", "01110,10001,10001,01111,00001,00010,01100,00000"),
        (":", "00000,01100,01100,00000,01100,01100,00000,00000"),
        (";", "00000,01100,01100,00000,01100,00100,01000,00000"),
        ("<", "00010,00100,01000,10000,01000,00100,00010,00000"),
        ("=", "00000,00000,11111,00000,11111,00000,00000,00000"),
        (">", "01000,00100,00010,00001,00010,00100,01000,00000"),
        ("?", "01110,10001,00001,00010,00100,00000,00100,00000"),
        ("@", "01110,10001,00001,01101,10101,10101,01110,00000"),
        ("A", "01110,10001,10001,10001,11111,10001,10001,00000"),
        ("B", "11110,10001,10001,11110,10001,10001,11110,00000"),
        ("C", "01110,10001,10000,10000,10000,10001,01110,00000"),
        ("D", "11100,10010,10001,10001,10001,10010,11100,00000"),
        ("E", "11111,10000,10000,11110,10000,10000,11111,00000"),
        ("F", "11111,10000,10000,11110,10000,10000,10000,00000"),
        ("G", "01110,10001,10000,10111,10001,10001,01111,00000"),
        ("H", "10001,10001,10001,11111,10001,10001,10001,00000"),
        ("I", "01110,00100,00100,00100,00100,00100,01110,00000"),
        ("J", "00111,00010,00010,00010,00010,10010,01100,00000"),
        ("K", "10001,10010,10100,11000,10100,10010,10001,00000"),
        ("L", "10000,10000,10000,10000,10000,10000,11111,00000"),
        ("M", "10001,11011,10101,10101,10001,10001,10001,00000"),
        ("N", "10001,10001,11001,10101,10011,10001,10001,00000"),
        ("O", "01110,10001,10001,10001,10001,10001,01110,00000"),
        ("P", "11110,10001,10001,11110,10000,10000,10000,00000"),
        ("Q", "01110,10001,10001,10001,10101,10010,01101,00000"),
        ("R", "11110,10001,10001,11110,10100,10010,10001,00000"),
        ("S", "01111,10000,10000,01110,00001,00001,11110,00000"),
        ("T", "11111,00100,00100,00100,00100,00100,00100,00000"),
        ("U", "10001,10001,10001,10001,10001,10001,01110,00000"),
        ("V", "10001,10001,10001,10001,10001,01010,00100,00000"),
        ("W", "10001,10001,10001,10101,10101,10101,01010,00000"),
        ("X", "10001,10001,01010,00100,01010,10001,10001,00000"),
        ("Y", "10001,10001,10001,01010,00100,00100,00100,00000"),
        ("Z", "11111,00001,00010,00100,01000,10000,11111,00000"),
        ("[", "01110,01000,01000,01000,01000,01000,01110,00000"),
        ("\\", "00000,10000,01000,00100,00010,00001,00000,00000"),
        ("]", "01110,00010,00010,00010,00010,00010,01110,00000"),
        ("^", "00100,01010,10001,00000,00000,00000,00000,00000"),
        ("_", "00000,00000,00000,00000,00000,00000,11111,00000"),
        ("`", "01000,00100,00010,00000,00000,00000,00000,00000"),
        ("a", "00000,00000,01110,00001,01111,10001,01111,00000"),
        ("b", "10000,10000,10110,11001,10001,10001,11110,00000"),
        ("c", "00000,00000,01110,10000,10000,10001,01110,00000"),
        ("d", "00001,00001,01101,10011,10001,10001,01111,00000"),
        ("e", "00000,00000,01110,10001,11111,10000,01110,00000"),
        ("f", "00110,01001,01000,11100,01000,01000,01000,00000"),
        ("g", "00000,00000,01111,10001,10001,01111,00001,01110"),
        ("h", "10000,10000,10110,11001,10001,10001,10001,00000"),
        ("i", "00100,00000,01100,00100,00100,00100,01110,00000"),
        ("j", "00010,00000,00110,00010,00010,00010,10010,01100"),
        ("k", "10000,10000,10010,10100,11000,10100,10010,00000"),
        ("l", "01100,00100,00100,00100,00100,00100,01110,00000"),
        ("m", "00000,00000,11010,10101,10101,10001,10001,00000"),
        ("n", "00000,00000,10110,11001,10001,10001,10001,00000"),
        ("o", "00000,00000,01110,10001,10001,10001,01110,00000"),
        ("p", "00000,00000,11110,10001,10001,11110,10000,10000"),
        ("q", "00000,00000,01111,10001,10001,01111,00001,00001"),
        ("r", "00000,00000,10110,11001,10000,10000,10000,00000"),
        ("s", "00000,00000,01111,10000,01110,00001,11110,00000"),
        ("t", "01000,01000,11100,01000,01000,01001,00110,00000"),
        ("u", "00000,00000,10001,10001,10001,10011,01101,00000"),
        ("v", "00000,00000,10001,10001,10001,01010,00100,00000"),
        ("w", "00000,00000,10001,10001,10101,10101,01010,00000"),
        ("x", "00000,00000,10001,01010,00100,01010,10001,00000"),
        ("y", "00000,00000,10001,10001,10001,01111,00001,01110"),
        ("z", "00000,00000,11111,00010,00100,01000,11111,00000"),
        ("{", "00010,00100,00100,01000,00100,00100,00010,00000"),
        ("|", "00100,00100,00100,00100,00100,00100,00100,00000"),
        ("}", "01000,00100,00100,00010,00100,00100,01000,00000"),
        ("~", "00000,00000,01000,10101,00010,00000,00000,00000"),
    ];

    // Marcas de acento: 2 linhas de 5 colunas.
    private static readonly Dictionary<char, (char Base, string Mark)> Accented = BuildAccents();

    private readonly Dictionary<char, int> _index = new();
    private readonly Texture2D _texture;
    private readonly Dictionary<int, BitmapGlyph>? _custom;
    private readonly int _lineHeight = CellHeight + 1;
    private readonly BitmapGlyph _fallback;

    private SpriteFont(Texture2D texture, Dictionary<char, int> index)
    {
        _texture = texture;
        _index = index;
    }

    private SpriteFont(Texture2D texture, Dictionary<int, BitmapGlyph> glyphs, int lineHeight, BitmapGlyph fallback)
    {
        _texture = texture; _custom = glyphs; _lineHeight = lineHeight; _fallback = fallback;
    }

    /// <summary>Cria uma fonte bitmap personalizada copiando o mapa de glifos.</summary>
    /// <param name="texture">Imagem/atlas compartilhado; o jogo ou ContentManager controla sua vida.</param>
    /// <param name="glyphs">Mapa de escalares Unicode (inteiros, não índices UTF-16) para recortes e métricas.</param>
    /// <param name="lineHeight">Passo vertical positivo entre linhas, em unidades da fonte.</param>
    /// <param name="fallbackCodePoint">Código presente no mapa usado para caracteres ausentes; padrão '?'.</param>
    /// <returns>Fonte imutável pronta para Measure e SpriteBatch.DrawString.</returns>
    /// <remarks>Não libera a textura. Glifos podem sobrepor/ultrapassar a linha por Offset; Measure informa
    /// avanço e passo de linhas, não o contorno da tinta. Sem kerning, shaping ou rasterização TrueType.</remarks>
    public static SpriteFont FromBitmap(Texture2D texture, IReadOnlyDictionary<int, BitmapGlyph> glyphs, int lineHeight, int fallbackCodePoint = '?')
    {
        ArgumentNullException.ThrowIfNull(texture); ArgumentNullException.ThrowIfNull(glyphs);
        if (texture.IsDisposed) throw new ObjectDisposedException(nameof(Texture2D));
        if (lineHeight <= 0) throw new ArgumentOutOfRangeException(nameof(lineHeight));
        if (!Rune.IsValid(fallbackCodePoint) || !glyphs.TryGetValue(fallbackCodePoint, out var fallback))
            throw new ArgumentException("O fallback deve ser um escalar Unicode presente no mapa.", nameof(fallbackCodePoint));
        var copy = new Dictionary<int, BitmapGlyph>(glyphs.Count);
        foreach (var (code, glyph) in glyphs)
        {
            var r = glyph.Source;
            if (!Rune.IsValid(code) || code is '\r' or '\n'
                || !float.IsFinite(r.X) || !float.IsFinite(r.Y) || !float.IsFinite(r.Width) || !float.IsFinite(r.Height)
                || r.X < 0 || r.Y < 0 || r.Width < 0 || r.Height < 0 || (r.Width == 0) != (r.Height == 0)
                || (double)r.X + r.Width > texture.Width || (double)r.Y + r.Height > texture.Height)
                throw new ArgumentException("Código Unicode ou região de glifo inválidos.", nameof(glyphs));
            copy.Add(code, glyph);
        }
        return new SpriteFont(texture, copy, lineHeight, fallback);
    }

    /// <summary>Altura de uma linha, em pixels da fonte (multiplicada por <c>scale</c> ao desenhar).</summary>
    public int LineHeight => _lineHeight;

    /// <summary>Cria a fonte bitmap embutida (5×7 pixels, com acentos do português).</summary>
    /// <param name="device">Dispositivo gráfico.</param>
    /// <returns>A fonte.</returns>
    public static SpriteFont CreateDefault(GraphicsDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        var glyphs = new List<(char Char, bool[,] Cell)>();
        foreach (var (text, rows) in Base) glyphs.Add((text[0], Rasterize(rows.Split(','))));
        foreach (var (accented, (baseChar, mark)) in Accented) glyphs.Add((accented, Compose(baseChar, mark)));

        var rowsCount = (glyphs.Count + Columns - 1) / Columns;
        var width = Columns * CellWidth;
        var height = rowsCount * CellHeight;
        var pixels = new byte[width * height * 4];
        var index = new Dictionary<char, int>();
        for (var i = 0; i < glyphs.Count; i++)
        {
            index[glyphs[i].Char] = i;
            var ox = i % Columns * CellWidth;
            var oy = i / Columns * CellHeight;
            for (var y = 0; y < CellHeight; y++)
            for (var x = 0; x < 5; x++)
            {
                if (!glyphs[i].Cell[y, x]) continue;
                var p = ((oy + y) * width + ox + x) * 4;
                pixels[p] = pixels[p + 1] = pixels[p + 2] = pixels[p + 3] = 255;
            }
        }
        return new SpriteFont(Texture2D.FromPixels(device, width, height, pixels, TextureFilter.Point), index);
    }

    /// <summary>Tamanho de layout do texto para a escala dada; em fontes personalizadas usa avanços e passo de linhas.</summary>
    /// <param name="text">Texto a desenhar.</param>
    /// <param name="scale">Fator de escala (1 = tamanho original).</param>
    /// <returns>Largura e altura do texto, em coordenadas virtuais.</returns>
    public Vector2 Measure(string text, float scale = 1f)
    {
        ArgumentNullException.ThrowIfNull(text);
        CheckScale(scale);
        if (_custom is not null) return MeasureCustom(text, scale);

        // CR, LF, CRLF e Unicode por escalar: mesma regra de FromBitmap.
        double lineWidth = 0, maxWidth = 0, lines = 1;
        bool afterCr = false;
        foreach (var rune in text.EnumerateRunes())
        {
            if (NewLine(rune.Value, ref afterCr))
            {
                maxWidth = Math.Max(maxWidth, lineWidth);
                lineWidth = 0;
                lines++;
            }
            else if (rune.Value != '\n')
                lineWidth += CellWidth;
        }
        double width = Math.Max(0, Math.Max(maxWidth, lineWidth) - 1) * scale;
        double height = lines * LineHeight * scale;
        if (width > float.MaxValue || height > float.MaxValue)
            throw new OverflowException("O layout não cabe em coordenadas float.");
        return new Vector2((float)width, (float)height);
    }

    internal void Draw(SpriteBatch batch, string text, Vector2 position, Color color, float scale)
    {
        ArgumentNullException.ThrowIfNull(text);
        CheckScale(scale);
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y))
            throw new ArgumentOutOfRangeException(nameof(position));
        if (_texture.IsDisposed) throw new ObjectDisposedException(nameof(Texture2D));
        if (_custom is not null) { DrawCustom(batch, text, position, color, scale); return; }

        double x = 0, y = 0;
        bool afterCr = false;
        foreach (var rune in text.EnumerateRunes())
        {
            if (NewLine(rune.Value, ref afterCr))
            {
                x = 0;
                y += LineHeight;
                continue;
            }
            if (rune.Value == '\n') continue;
            char code = rune.Value <= char.MaxValue ? (char)rune.Value : '?';
            var i = _index.TryGetValue(code, out var found) ? found : _index['?'];
            if (code != ' ')
            {
                double left = position.X + x * scale, top = position.Y + y * scale;
                double width = 5.0 * scale, height = CellHeight * (double)scale;
                if (left >= -float.MaxValue && top >= -float.MaxValue
                    && left + width <= float.MaxValue && top + height <= float.MaxValue
                    && width <= float.MaxValue && height <= float.MaxValue)
                {
                    var source = new RectangleF(i % Columns * CellWidth, i / Columns * CellHeight, 5, CellHeight);
                    batch.Draw(_texture, new RectangleF((float)left, (float)top, (float)width, (float)height),
                        source, color, 0f, Vector2.Zero);
                }
            }
            x += CellWidth;
        }
    }

    private static void CheckScale(float scale)
    {
        if (!float.IsFinite(scale) || scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
    }

    private BitmapGlyph GetGlyph(int code) => _custom!.TryGetValue(code, out var glyph) ? glyph : _fallback;

    private static bool NewLine(int code, ref bool afterCr)
    {
        bool line = code == '\r' || (code == '\n' && !afterCr);
        afterCr = code == '\r';
        return line;
    }

    private Vector2 MeasureCustom(string text, float scale)
    {
        CheckScale(scale);
        double width = 0, max = 0, lines = 1;
        bool afterCr = false;
        foreach (var rune in text.EnumerateRunes())
        {
            if (NewLine(rune.Value, ref afterCr)) { max = Math.Max(max, width); width = 0; lines++; }
            else if (rune.Value != '\n') width += GetGlyph(rune.Value).Advance;
        }
        double x = Math.Max(max, width) * scale, y = lines * LineHeight * scale;
        if (x > float.MaxValue || y > float.MaxValue) throw new OverflowException("O layout não cabe em coordenadas float.");
        return new((float)x, (float)y);
    }

    private void DrawCustom(SpriteBatch batch, string text, Vector2 position, Color color, float scale)
    {
        ArgumentNullException.ThrowIfNull(text); CheckScale(scale);
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y)) throw new ArgumentOutOfRangeException(nameof(position));
        if (_texture.IsDisposed) throw new ObjectDisposedException(nameof(Texture2D));
        double x = 0, y = 0;
        bool afterCr = false;
        foreach (var rune in text.EnumerateRunes())
        {
            if (NewLine(rune.Value, ref afterCr)) { x = 0; y += LineHeight; continue; }
            if (rune.Value == '\n') continue;
            var glyph = GetGlyph(rune.Value);
            double left = position.X + (x + glyph.Offset.X) * scale;
            double top = position.Y + (y + glyph.Offset.Y) * scale;
            double width = (double)glyph.Source.Width * scale, height = (double)glyph.Source.Height * scale;
            // Layout extremo continua avançando, mas não envia Inf/NaN à GPU.
            if (width > 0 && height > 0 && width <= float.MaxValue && height <= float.MaxValue
                && left >= -float.MaxValue && top >= -float.MaxValue
                && left + width <= float.MaxValue && top + height <= float.MaxValue)
                batch.Draw(_texture, new RectangleF((float)left, (float)top, (float)width, (float)height), glyph.Source, color, 0, Vector2.Zero);
            x += glyph.Advance;
        }
    }

    private static bool[,] Rasterize(string[] rows)
    {
        // Linhas 0..7 do glifo ficam nas linhas 2..9 da célula.
        var cell = new bool[CellHeight, 5];
        for (var r = 0; r < rows.Length; r++)
        for (var x = 0; x < 5; x++)
            cell[r + 2, x] = rows[r][x] == '1';
        return cell;
    }

    private static bool[,] Compose(char baseChar, string mark)
    {
        var baseRows = Base.First(b => b.Char[0] == baseChar).Rows.Split(',');
        var cell = Rasterize(baseRows);
        var markRows = mark.Split(',');
        if (mark.Length == 5) // cedilha: gancho abaixo (última linha do glifo)
        {
            for (var x = 0; x < 5; x++) cell[9, x] = mark[x] == '1';
            return cell;
        }
        // Minúsculas: a marca substitui as duas primeiras linhas do glifo (pontos de i, etc.); maiúsculas: fica acima.
        var top = char.IsLower(baseChar) ? 2 : 0;
        for (var r = 0; r < 2; r++)
        for (var x = 0; x < 5; x++)
            cell[top + r, x] = markRows[r][x] == '1';
        return cell;
    }

    private static Dictionary<char, (char, string)> BuildAccents()
    {
        const string acute = "00010,00100", grave = "01000,00100", circ = "00100,01010", tilde = "01101,10110", diaer = "01010,00000";
        var map = new Dictionary<char, (char, string)>();
        void Add(string chars, string bases, string mark)
        {
            for (var i = 0; i < chars.Length; i++) map[chars[i]] = (bases[i], mark);
        }
        Add("áéíóúÁÉÍÓÚ", "aeiouAEIOU", acute);
        Add("àèìòùÀÈÌÒÙ", "aeiouAEIOU", grave);
        Add("âêîôûÂÊÎÔÛ", "aeiouAEIOU", circ);
        Add("ãõñÃÕÑ", "aonAON", tilde);
        Add("äëïöüÄËÏÖÜ", "aeiouAEIOU", diaer);
        map['ç'] = ('c', "00100");
        map['Ç'] = ('C', "00100");
        return map;
    }
}
