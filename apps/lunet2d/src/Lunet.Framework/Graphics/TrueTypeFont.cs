using System.Numerics;
using System.Text;
using StbTrueTypeSharp;

namespace Lunet.Graphics;

/// <summary>Atlas de glifos TrueType gerado offline, compatível com SpriteBatch.DrawString.</summary>
/// <example><code>
/// var baked = content.LoadTrueTypeFont("Fonts/ui.ttf", 28);
/// batch.Begin();
/// batch.DrawString(baked.Font, "Olá!", new Vector2(16, 16), Color.White);
/// batch.End();
/// </code></example>
/// <remarks>Rasterização feita apenas ao carregar, sem serviço de rede nem dependência de código nativo do Android.
/// O jogo fornece os bytes .ttf e controla a vida do objeto. Libere-o ao descarregar o jogo.
/// Não oferece shaping complexo, kerning, fontes coloridas ou OTF/CFF; glifos ausentes usam '?'.
/// O SpriteFont antigo continua compatível e não assume a posse de outras texturas.</remarks>
public sealed class TrueTypeFont : IDisposable
{
    /// <summary>Conjunto inicial para texto comum, incluindo caracteres portugueses e símbolos monetários.</summary>
    private const string StandardCharset =
        " !\\\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
        "ÁÀÂÃÄÇÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÝÑáàâãäçéèêëíìîïóòôõöúùûüýÿñºª€£¥°–—…";

    /// <summary>Conjunto inicial de caracteres para fontes TTF, com ASCII e português.</summary>
    public static string DefaultCharacters => StandardCharset;

    private TrueTypeFont(Texture2D atlas, SpriteFont font)
    {
        Atlas = atlas;
        Font = font;
    }

    /// <summary>Fonte proporcional pronta para Measure e SpriteBatch.DrawString.</summary>
    public SpriteFont Font { get; }

    /// <summary>Textura RGBA compartilhada pelos glifos; propriedade deste objeto.</summary>
    public Texture2D Atlas { get; }

    /// <summary>Verdadeiro após a liberação da textura.</summary>
    public bool IsDisposed => Atlas.IsDisposed;

    /// <summary>Rasteriza um arquivo TTF em atlas limitado, sem novas alocações na medição/desenho de cada quadro.</summary>
    /// <param name="device">Dispositivo gráfico do jogo.</param>
    /// <param name="ttfBytes">Conteúdo binário TTF; não é retido depois da criação.</param>
    /// <param name="pixelHeight">Altura nominal entre 4 e 192 pixels virtuais.</param>
    /// <param name="characters">Escalares Unicode que serão pré-rasterizados; '?' é sempre incluído.</param>
    /// <param name="atlasSize">Lado da textura quadrada, entre 128 e 2048 pixels, máximo 16 MiB de RGBA.</param>
    /// <returns>Fonte e atlas que devem ser descartados juntos.</returns>
    /// <remarks>O tamanho da textura e o conjunto de glifos são definidos no LoadContent; mudar requer novo Bake.
    /// Não baixa fontes, não gera arquivos e não modifica os projetos existentes. Se os glifos não couberem,
    /// rejeita explicitamente em vez de criar caracteres invisíveis.</remarks>
    public static unsafe TrueTypeFont Bake(GraphicsDevice device, byte[] ttfBytes, float pixelHeight,
        string? characters = null, int atlasSize = 1024)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(ttfBytes);
        characters ??= StandardCharset;
        if (ttfBytes.Length is < 12 or > 8_388_608)
            throw new ArgumentOutOfRangeException(nameof(ttfBytes), "TTF deve ter entre 12 bytes e 8 MiB.");
        if (!float.IsFinite(pixelHeight) || pixelHeight < 4 || pixelHeight > 192)
            throw new ArgumentOutOfRangeException(nameof(pixelHeight));
        if (atlasSize < 128 || atlasSize > 2048)
            throw new ArgumentOutOfRangeException(nameof(atlasSize));

        var wanted = new SortedSet<int> { '?' };
        foreach (var rune in characters.EnumerateRunes())
        {
            if (rune.Value is '\r' or '\n') continue;
            wanted.Add(rune.Value);
            if (wanted.Count > 1024) throw new ArgumentOutOfRangeException(nameof(characters), "No máximo 1024 glifos.");
        }

        using var fontInfo = StbTrueType.CreateFont(ttfBytes, 0)
            ?? throw new InvalidDataException("Dados não representam uma fonte TrueType suportada.");
        if (StbTrueType.stbtt_FindGlyphIndex(fontInfo, '?') == 0)
            throw new InvalidDataException("Fonte não contém o glifo de fallback '?'.");
        var valid = new List<int>(wanted.Count);
        foreach (int codePoint in wanted)
            if (StbTrueType.stbtt_FindGlyphIndex(fontInfo, codePoint) != 0)
                valid.Add(codePoint);

        float scale = StbTrueType.stbtt_ScaleForPixelHeight(fontInfo, pixelHeight);
        int ascent, descent, gap;
        StbTrueType.stbtt_GetFontVMetrics(fontInfo, &ascent, &descent, &gap);
        double line = Math.Ceiling(((double)ascent - descent + Math.Max(gap, 0)) * scale);
        double baseline = ascent * (double)scale;
        if (!double.IsFinite(line) || line < 1 || line > 4096 || !double.IsFinite(baseline))
            throw new InvalidDataException("Métricas verticais da fonte são inválidas.");

        int size = checked(atlasSize * atlasSize);
        byte[] alpha = new byte[size];
        var points = valid.ToArray();
        var packed = new StbTrueType.stbtt_packedchar[points.Length];
        var context = new StbTrueType.stbtt_pack_context();
        fixed (byte* ttf = ttfBytes)
        fixed (byte* dest = alpha)
        fixed (int* codes = points)
        fixed (StbTrueType.stbtt_packedchar* glyphs = packed)
        {
            if (StbTrueType.stbtt_PackBegin(context, dest, atlasSize, atlasSize, atlasSize, 1, null) == 0)
                throw new InvalidOperationException("Não foi possível criar o atlas TTF.");
            try
            {
                var range = new StbTrueType.stbtt_pack_range
                {
                    font_size = pixelHeight,
                    first_unicode_codepoint_in_range = 0,
                    array_of_unicode_codepoints = codes,
                    num_chars = points.Length,
                    chardata_for_range = glyphs
                };
                if (StbTrueType.stbtt_PackFontRanges(context, ttf, 0, &range, 1) == 0)
                    throw new InvalidOperationException("Atlas pequeno demais para a fonte e os caracteres escolhidos.");
            }
            finally { StbTrueType.stbtt_PackEnd(context); }
        }

        var map = new Dictionary<int, BitmapGlyph>(points.Length);
        for (int i = 0; i < points.Length; i++)
        {
            var glyph = packed[i];
            int width = glyph.x1 - glyph.x0, height = glyph.y1 - glyph.y0;
            if (!float.IsFinite(glyph.xadvance) || glyph.xadvance < 0 ||
                !float.IsFinite(glyph.xoff) || !float.IsFinite(glyph.yoff))
                throw new InvalidDataException("Glifo inválido no TTF.");
            var region = width == 0 || height == 0
                ? default
                : new RectangleF(glyph.x0, glyph.y0, width, height);
            map.Add(points[i], new BitmapGlyph(region, glyph.xadvance,
                new Vector2(glyph.xoff, (float)(glyph.yoff + baseline))));
        }

        var rgba = new byte[checked(size * 4)];
        for (int i = 0; i < size; i++)
        {
            int dest = i * 4;
            rgba[dest] = rgba[dest + 1] = rgba[dest + 2] = 255;
            rgba[dest + 3] = alpha[i];
        }
        var texture = Texture2D.FromPixels(device, atlasSize, atlasSize, rgba, TextureFilter.Linear);
        try { return new TrueTypeFont(texture, SpriteFont.FromBitmap(texture, map, (int)line)); }
        catch { texture.Dispose(); throw; }
    }

    /// <summary>Libera a textura do atlas da GPU; não libera o GraphicsDevice.</summary>
    public void Dispose() => Atlas.Dispose();
}
