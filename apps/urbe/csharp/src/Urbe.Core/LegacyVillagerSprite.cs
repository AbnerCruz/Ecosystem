namespace Urbe.Core;

/// <summary>
/// pixel-art.js villager(look, dir, frame): medieval little people with a big head, 12×18 grid
/// plus a 1 px dark outline (14×20), looking right on 'side'. Ported line by line.
/// </summary>
public static class LegacyVillagerSprite
{
    public const int Width = 14, Height = 20;
    private const int W = 12, H = 18;
    private static readonly string[] Skins = ["#f1c9a5", "#e0ac85", "#c68b62", "#9a6644", "#6f4a33"];
    private static readonly string[] Hairs = ["#2b211c", "#5a3b24", "#8a5a2c", "#c9a063", "#b8b2a8", "#7a2e1f"];
    private static readonly string[] PantsColors = ["#5a4a3a", "#3f4a5a", "#6b5a44", "#4a5a3f", "#5c3f3a"];

    private readonly record struct Rgb(int R, int G, int B);

    private static Rgb Hex(string h)
    {
        var (r, g, b) = LegacyLifeSprites.Hex(h);
        return new Rgb(r, g, b);
    }

    private static Rgb Shade(Rgb c, double f) =>
        new((int)LegacyJsMath.Round(Math.Min(255, c.R * f)), (int)LegacyJsMath.Round(Math.Min(255, c.G * f)), (int)LegacyJsMath.Round(Math.Min(255, c.B * f)));

    /// <summary>RGBA 14×20; <paramref name="dir"/> is down, up or side; flip mirrors (villagerFlip).</summary>
    public static byte[] Render(LegacyVillagerLook look, string dir, int frame, bool flip = false)
    {
        var g = new Rgb?[W * H];
        void R(int x, int y, int w, int h, Rgb c)
        {
            for (int j = 0; j < h; j++)
            for (int k = 0; k < w; k++)
            {
                int xx = x + k, yy = y + j;
                if (xx >= 0 && yy >= 0 && xx < W && yy < H) g[yy * W + xx] = c;
            }
        }
        void P(int x, int y, Rgb c) => R(x, y, 1, 1, c);

        Rgb skin = Hex(Skins[look.Skin % Skins.Length]), skinD = Shade(skin, .85), hair = Hex(Hairs[look.Hair % Hairs.Length]), hairL = Shade(hair, 1.25);
        Rgb shirt = Hex(look.Shirt), shirtD = Shade(shirt, .78), shirtL = Shade(shirt, 1.12);
        Rgb pants = Hex(PantsColors[look.Pants % PantsColors.Length]), shoe = Hex("#3a2a20"), belt = Hex("#5a3e27"), eye = Hex("#1c1a1a");
        string st = look.Style;
        int sw = new[] { 0, 1, 0, -1 }[frame & 3];
        bool side = dir == "side", back = dir == "up";
        // legs and shoes
        if (look.Dress)
        {
            R(side ? 4 : 3, 11, side ? 5 : 6, 4, shirtD);
            R(side ? 4 : 3, 14, side ? 5 : 6, 1, Shade(shirt, .65));
            if (side)
            {
                R(sw > 0 ? 4 : sw < 0 ? 7 : 5, 15, 1, 2, skinD);
                R(sw > 0 ? 7 : sw < 0 ? 4 : 6, 15, 1, 2, skinD);
                R(sw > 0 ? 3 : sw < 0 ? 7 : 5, 17, 2, 1, shoe);
                R(sw > 0 ? 7 : sw < 0 ? 3 : 6, 17, 1, 1, shoe);
            }
            else
            {
                R(4, 15, 1, 2, skinD);
                R(7, 15, 1, 2, skinD);
                R(3, 17 - (sw > 0 ? 1 : 0), 2, 1, shoe);
                R(7, 17 - (sw < 0 ? 1 : 0), 2, 1, shoe);
            }
        }
        else if (side)
        {
            int fa = sw > 0 ? 6 : sw < 0 ? 4 : 5, fb = sw > 0 ? 4 : sw < 0 ? 6 : 5;
            R(fb, 13, 2, 4, Shade(pants, .8));
            R(fa, 13, 2, 4, pants);
            R(fb, 17, 2, 1, shoe);
            R(fa + (sw != 0 ? 1 : 0), 17, 2, 1, shoe);
        }
        else
        {
            int la = sw > 0 ? 1 : 0, ra = sw < 0 ? 1 : 0;
            R(4, 13, 2, 4 - la, pants);
            R(6, 13, 2, 4 - ra, Shade(pants, .85));
            R(4, 17 - la, 2, 1, shoe);
            R(6, 17 - ra, 2, 1, shoe);
        }
        // trunk, belt and arms
        if (side)
        {
            R(4, 7, 4, 5, shirt);
            R(4, 7, 1, 5, shirtL);
            R(7, 7, 1, 5, shirtD);
            R(4, 11, 4, 1, look.Dress ? shirtD : belt);
            int ax = 5 + (sw > 0 ? 1 : sw < 0 ? -1 : 0);
            R(ax, 7, 2, 4, shirtD);
            P(ax + (sw > 0 ? 1 : 0), 11, skin);
        }
        else
        {
            R(3, 7, 6, 5, shirt);
            R(3, 7, 1, 5, shirtL);
            R(8, 7, 1, 5, shirtD);
            if (!back) { P(5, 7, shirtD); P(6, 7, shirtD); }
            R(3, 11, 6, 1, look.Dress ? shirtD : belt);
            R(2, 7 + (sw < 0 ? 1 : 0), 1, 4, shirtD);
            R(9, 7 + (sw > 0 ? 1 : 0), 1, 4, shirtD);
            P(2, 11 + (sw < 0 ? 1 : 0), skin);
            P(9, 11 + (sw > 0 ? 1 : 0), skin);
        }
        // neck and head
        R(5, 6, 2, 1, skinD);
        R(3, 1, 6, 5, skin);
        R(3, 5, 6, 1, skinD);
        if (side) { P(7, 3, eye); P(8, 4, skinD); }
        else if (!back) { P(4, 3, eye); P(7, 3, eye); P(5, 5, Shade(skin, .9)); P(6, 5, Shade(skin, .9)); }
        // hair / hat
        if (st == "bald")
        {
            R(3, 1, 6, 1, skinD);
            if (side) R(3, 2, 2, 3, hair);
            else { R(3, 3, 1, 2, hair); R(8, 3, 1, 2, hair); }
            if (back) R(3, 3, 6, 3, hair);
        }
        else
        {
            bool longHair = st == "long";
            R(3, 0, 6, 2, hair);
            R(3, 0, 6, 1, hairL);
            if (back) R(3, 1, 6, longHair ? 7 : 5, hair);
            else if (side) { R(3, 1, 3, longHair ? 6 : 4, hair); P(8, 1, hair); }
            else { R(3, 1, 1, longHair ? 6 : 3, hair); R(8, 1, 1, longHair ? 6 : 3, hair); }
        }
        if (st == "straw")
        {
            Rgb hs = Hex("#d8b765"), hsD = Hex("#b38f45");
            R(3, -1, 6, 1, hs);
            R(3, 0, 6, 1, hs);
            R(3, 1, 6, 1, Hex("#a4552f"));
            R(1, 2, 10, 1, hsD);
            if (side) R(1, 2, 11, 1, hsD);
        }
        else if (st == "hood")
        {
            Rgb hc = Hex(look.Hood ?? "#5b4a6a"), hcD = Shade(hc, .75);
            R(2, 0, 8, 6, hc);
            R(2, 0, 8, 1, Shade(hc, 1.15));
            if (!back) { R(4, 2, 4, 4, skin); P(4, 3, eye); P(7, 3, eye); }
            else R(3, 1, 6, 5, hcD);
            if (side) { R(2, 0, 5, 6, hc); R(6, 2, 3, 4, skin); P(7, 3, eye); }
            R(2, 6, 8, 2, hc);
        }
        else if (st == "cap")
        {
            Rgb cc = Hex(look.Cap ?? "#a33c32");
            R(3, 0, 6, 2, cc);
            R(3, 0, 6, 1, Shade(cc, 1.2));
            if (!back) R(side ? 7 : 2, 1, side ? 3 : 8, 1, Shade(cc, .8));
        }
        // accessories
        if (look.Acc == "basket")
        {
            Rgb bk = Hex("#a8763e"), bkD = Hex("#7a5228");
            if (side) { R(7, 9, 3, 3, bk); R(7, 9, 3, 1, bkD); P(8, 8, bkD); }
            else { R(9, 9, 3, 3, bk); R(9, 9, 3, 1, bkD); }
        }
        else if (look.Acc == "sack")
        {
            Rgb sk = Hex("#c8b48a");
            if (back || !side) { R(back ? 4 : 8, 5, 4, 4, sk); R(back ? 4 : 8, 5, 4, 1, Shade(sk, .8)); }
            else R(2, 5, 3, 5, sk);
        }
        else if (look.Acc == "staff")
        {
            R(side ? 9 : 10, 2, 1, 15, Hex("#7a5a3a"));
            P(side ? 9 : 10, 2, Hex("#9a7a52"));
        }
        else if (look.Acc == "bucket")
        {
            R(side ? 7 : 9, 11, 3, 3, Hex("#8c8a86"));
            R(side ? 7 : 9, 11, 3, 1, Hex("#b0aea9"));
        }
        // outline
        var d = new byte[Width * Height * 4];
        bool Has(int i, int j) => i >= 0 && j >= 0 && i < W && j < H && g[j * W + i] is not null;
        for (int j = -1; j <= H; j++)
        for (int i = -1; i <= W; i++)
        {
            int ox = flip ? Width - 1 - (i + 1) : i + 1;
            int o = ((j + 1) * Width + ox) * 4;
            if (Has(i, j))
            {
                var c = g[j * W + i]!.Value;
                d[o] = (byte)c.R; d[o + 1] = (byte)c.G; d[o + 2] = (byte)c.B; d[o + 3] = 255;
            }
            else if (Has(i - 1, j) || Has(i + 1, j) || Has(i, j - 1) || Has(i, j + 1))
            {
                d[o] = 34; d[o + 1] = 26; d[o + 2] = 22; d[o + 3] = 230;
            }
        }
        return d;
    }
}
