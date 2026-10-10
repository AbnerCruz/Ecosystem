namespace Urbe.Core;

/// <summary>
/// Moradores da 1.8.4-beta: porta literal de <c>villager()</c> e <c>villagerFlip()</c> de
/// src/world/pixel-art.js. Grade 12×18 com 1 px de contorno escuro (14×20), RGBA sem pré-multiplicação.
/// Provada contra o JS original por oráculo (tools/villager-oracle.mjs); nenhum JS entra no app.
/// </summary>
public static class LegacyVillagerSprite
{
    public const int Width = 14;
    public const int Height = 20;

    /// <summary>look do JS: pele/cabelo/calça são índices; shirt/cap/hood são "#rrggbb".</summary>
    public sealed record Look(int Skin, int Hair, int Pants, string Style, string Shirt,
        bool Dress = false, string? Cap = null, string? Hood = null, string? Acc = null);

    private static readonly string[] SkinColors = ["#f1c9a5", "#e0ac85", "#c68b62", "#9a6644", "#6f4a33"];
    private static readonly string[] HairColors = ["#2b211c", "#5a3b24", "#8a5a2c", "#c9a063", "#b8b2a8", "#7a2e1f"];
    private static readonly string[] PantsColors = ["#5a4a3a", "#3f4a5a", "#6b5a44", "#4a5a3f", "#5c3f3a"];
    private static readonly (int R, int G, int B) Outline = (34, 26, 22);

    private readonly record struct Rgb(int R, int G, int B);

    // JS Math.round = floor(v + .5); Math.min(255, ...) antes de arredondar.
    private static Rgb Shade(string hex, double f)
    {
        var c = Parse(hex);
        static int S(int v, double f) => (int)Math.Floor(Math.Min(255, v * f) + .5);
        return new Rgb(S(c.R, f), S(c.G, f), S(c.B, f));
    }
    private static Rgb Parse(string hex)
    {
        hex = hex.TrimStart('#');
        return new Rgb(Convert.ToInt32(hex[..2], 16), Convert.ToInt32(hex[2..4], 16), Convert.ToInt32(hex[4..6], 16));
    }
    private static int Mod(int n, int m) => ((n % m) + m) % m;

    private sealed class Grid
    {
        private const int W = 12, H = 18;
        private readonly Rgb?[] _g = new Rgb?[W * H];
        public void R(int x, int y, int w, int h, Rgb c)
        {
            for (var j = 0; j < h; j++)
                for (var k = 0; k < w; k++)
                {
                    int xx = x + k, yy = y + j;
                    if (xx >= 0 && yy >= 0 && xx < W && yy < H) _g[yy * W + xx] = c;
                }
        }
        public void P(int x, int y, Rgb c) => R(x, y, 1, 1, c);
        public bool Has(int i, int j) => i >= 0 && j >= 0 && i < W && j < H && _g[j * W + i] is not null;
        public Rgb At(int i, int j) => _g[j * W + i]!.Value;
    }

    /// <param name="dir">"down", "up" ou "side" (olhando para a direita).</param>
    public static byte[] Create(Look look, string dir, int frame)
    {
        ArgumentNullException.ThrowIfNull(look);
        if (dir is not ("down" or "up" or "side")) throw new ArgumentException("Direção desconhecida.", nameof(dir));
        var g = new Grid();
        var skin = Parse(SkinColors[Mod(look.Skin, SkinColors.Length)]);
        var skinHex = SkinColors[Mod(look.Skin, SkinColors.Length)];
        var skinD = Shade(skinHex, .85);
        var hairHex = HairColors[Mod(look.Hair, HairColors.Length)];
        var hair = Parse(hairHex);
        var hairL = Shade(hairHex, 1.25);
        var shirt = Parse(look.Shirt);
        var shirtD = Shade(look.Shirt, .78);
        var shirtL = Shade(look.Shirt, 1.12);
        var pantsHex = PantsColors[Mod(look.Pants, PantsColors.Length)];
        var pants = Parse(pantsHex);
        var shoe = Parse("#3a2a20");
        var belt = Parse("#5a3e27");
        var eye = Parse("#1c1a1a");
        var st = look.Style;
        int sw = new[] { 0, 1, 0, -1 }[frame & 3];
        bool side = dir == "side", back = dir == "up";

        // pernas e sapatos
        if (look.Dress)
        {
            g.R(side ? 4 : 3, 11, side ? 5 : 6, 4, shirtD);
            g.R(side ? 4 : 3, 14, side ? 5 : 6, 1, Shade(look.Shirt, .65));
            if (side)
            {
                g.R(sw > 0 ? 4 : sw < 0 ? 7 : 5, 15, 1, 2, skinD);
                g.R(sw > 0 ? 7 : sw < 0 ? 4 : 6, 15, 1, 2, skinD);
                g.R(sw > 0 ? 3 : sw < 0 ? 7 : 5, 17, 2, 1, shoe);
                g.R(sw > 0 ? 7 : sw < 0 ? 3 : 6, 17, 1, 1, shoe);
            }
            else
            {
                g.R(4, 15, 1, 2, skinD);
                g.R(7, 15, 1, 2, skinD);
                g.R(3, 17 - (sw > 0 ? 1 : 0), 2, 1, shoe);
                g.R(7, 17 - (sw < 0 ? 1 : 0), 2, 1, shoe);
            }
        }
        else if (side)
        {
            int fa = sw > 0 ? 6 : sw < 0 ? 4 : 5, fb = sw > 0 ? 4 : sw < 0 ? 6 : 5;
            g.R(fb, 13, 2, 4, Shade(pantsHex, .8));
            g.R(fa, 13, 2, 4, pants);
            g.R(fb, 17, 2, 1, shoe);
            g.R(fa + (sw != 0 ? 1 : 0), 17, 2, 1, shoe);
        }
        else
        {
            int la = sw > 0 ? 1 : 0, ra = sw < 0 ? 1 : 0;
            g.R(4, 13, 2, 4 - la, pants);
            g.R(6, 13, 2, 4 - ra, Shade(pantsHex, .85));
            g.R(4, 17 - la, 2, 1, shoe);
            g.R(6, 17 - ra, 2, 1, shoe);
        }

        // tronco, cinto e braços
        if (side)
        {
            g.R(4, 7, 4, 5, shirt);
            g.R(4, 7, 1, 5, shirtL);
            g.R(7, 7, 1, 5, shirtD);
            g.R(4, 11, 4, 1, look.Dress ? shirtD : belt);
            int ax = 5 + (sw > 0 ? 1 : sw < 0 ? -1 : 0);
            g.R(ax, 7, 2, 4, shirtD);
            g.P(ax + (sw > 0 ? 1 : 0), 11, skin);
        }
        else
        {
            g.R(3, 7, 6, 5, shirt);
            g.R(3, 7, 1, 5, shirtL);
            g.R(8, 7, 1, 5, shirtD);
            if (!back) { g.P(5, 7, shirtD); g.P(6, 7, shirtD); }
            g.R(3, 11, 6, 1, look.Dress ? shirtD : belt);
            g.R(2, 7 + (sw < 0 ? 1 : 0), 1, 4, shirtD);
            g.R(9, 7 + (sw > 0 ? 1 : 0), 1, 4, shirtD);
            g.P(2, 11 + (sw < 0 ? 1 : 0), skin);
            g.P(9, 11 + (sw > 0 ? 1 : 0), skin);
        }

        // pescoço e cabeça
        g.R(5, 6, 2, 1, skinD);
        g.R(3, 1, 6, 5, skin);
        g.R(3, 5, 6, 1, skinD);
        if (side) { g.P(7, 3, eye); g.P(8, 4, skinD); }
        else if (!back)
        {
            g.P(4, 3, eye); g.P(7, 3, eye);
            g.P(5, 5, Shade(skinHex, .9)); g.P(6, 5, Shade(skinHex, .9));
        }

        // cabelo / chapéu
        if (st == "bald")
        {
            g.R(3, 1, 6, 1, skinD);
            if (side) g.R(3, 2, 2, 3, hair);
            else { g.R(3, 3, 1, 2, hair); g.R(8, 3, 1, 2, hair); }
            if (back) g.R(3, 3, 6, 3, hair);
        }
        else
        {
            bool longo = st == "long";
            g.R(3, 0, 6, 2, hair);
            g.R(3, 0, 6, 1, hairL);
            if (back) g.R(3, 1, 6, longo ? 7 : 5, hair);
            else if (side) { g.R(3, 1, 3, longo ? 6 : 4, hair); g.P(8, 1, hair); }
            else { g.R(3, 1, 1, longo ? 6 : 3, hair); g.R(8, 1, 1, longo ? 6 : 3, hair); }
        }
        if (st == "straw")
        {
            var hs = Parse("#d8b765"); var hsD = Parse("#b38f45");
            g.R(3, -1, 6, 1, hs); g.R(3, 0, 6, 1, hs); g.R(3, 1, 6, 1, Parse("#a4552f"));
            g.R(1, 2, 10, 1, hsD);
            if (side) g.R(1, 2, 11, 1, hsD);
        }
        else if (st == "hood")
        {
            var hcHex = look.Hood ?? "#5b4a6a";
            var hc = Parse(hcHex); var hcD = Shade(hcHex, .75);
            g.R(2, 0, 8, 6, hc);
            g.R(2, 0, 8, 1, Shade(hcHex, 1.15));
            if (!back) { g.R(4, 2, 4, 4, skin); g.P(4, 3, eye); g.P(7, 3, eye); }
            else g.R(3, 1, 6, 5, hcD);
            if (side) { g.R(2, 0, 5, 6, hc); g.R(6, 2, 3, 4, skin); g.P(7, 3, eye); }
            g.R(2, 6, 8, 2, hc);
        }
        else if (st == "cap")
        {
            var ccHex = look.Cap ?? "#a33c32";
            var cc = Parse(ccHex);
            g.R(3, 0, 6, 2, cc);
            g.R(3, 0, 6, 1, Shade(ccHex, 1.2));
            if (!back) g.R(side ? 7 : 2, 1, side ? 3 : 8, 1, Shade(ccHex, .8));
        }

        // acessórios
        switch (look.Acc)
        {
            case "basket":
                var bk = Parse("#a8763e"); var bkD = Parse("#7a5228");
                if (side) { g.R(7, 9, 3, 3, bk); g.R(7, 9, 3, 1, bkD); g.P(8, 8, bkD); }
                else { g.R(9, 9, 3, 3, bk); g.R(9, 9, 3, 1, bkD); }
                break;
            case "sack":
                var skHex = "#c8b48a"; var sk = Parse(skHex);
                if (back || !side)
                {
                    g.R(back ? 4 : 8, 5, 4, 4, sk);
                    g.R(back ? 4 : 8, 5, 4, 1, Shade(skHex, .8));
                }
                else g.R(2, 5, 3, 5, sk);
                break;
            case "staff":
                g.R(side ? 9 : 10, 2, 1, 15, Parse("#7a5a3a"));
                g.P(side ? 9 : 10, 2, Parse("#9a7a52"));
                break;
            case "bucket":
                g.R(side ? 7 : 9, 11, 3, 3, Parse("#8c8a86"));
                g.R(side ? 7 : 9, 11, 3, 1, Parse("#b0aea9"));
                break;
        }

        // contorno: 1 px escuro (alfa 230) em volta de tudo que foi pintado
        var rgba = new byte[Width * Height * 4];
        for (var j = -1; j <= 18; j++)
            for (var i = -1; i <= 12; i++)
            {
                var o = ((j + 1) * Width + (i + 1)) * 4;
                if (g.Has(i, j))
                {
                    var c = g.At(i, j);
                    rgba[o] = (byte)c.R; rgba[o + 1] = (byte)c.G; rgba[o + 2] = (byte)c.B; rgba[o + 3] = 255;
                }
                else if (g.Has(i - 1, j) || g.Has(i + 1, j) || g.Has(i, j - 1) || g.Has(i, j + 1))
                {
                    rgba[o] = (byte)Outline.Item1; rgba[o + 1] = (byte)Outline.Item2;
                    rgba[o + 2] = (byte)Outline.Item3; rgba[o + 3] = 230;
                }
            }
        return rgba;
    }

    /// <summary>villagerFlip: o quadro 'side' espelhado na horizontal.</summary>
    public static byte[] CreateFlipped(Look look, int frame)
    {
        var src = Create(look, "side", frame);
        var dst = new byte[src.Length];
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
                Buffer.BlockCopy(src, (y * Width + x) * 4, dst, (y * Width + (Width - 1 - x)) * 4, 4);
        return dst;
    }
}
