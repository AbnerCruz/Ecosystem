namespace Urbe.Core;

/// <summary>
/// The pixel art of the life of the world, as RGBA rasters: pixel-art.js ANIMALS/animal()
/// and the life.js grid sprites (pigeon, dog, fox, boat, fish), hot-air balloon
/// (balaoImg), glow (brilhoImg) and cloud puffs (nuvemImg). Grids and colours verbatim.
/// </summary>
public static class LegacyLifeSprites
{
    public sealed record Grid(string[][] Frames, IReadOnlyDictionary<char, string> Palette);

    /// <summary>pixel-art.js ANIMALS (looking right).</summary>
    public static readonly IReadOnlyDictionary<string, Grid> Animals = new Dictionary<string, Grid>
    {
        ["sheep"] = new([
            ["....ooooo.....", "..oowwwwwoo...", ".owwwwwwwwwokk", ".owwwwwwwwwkek", "..oWWwwwwWWokk", "...ooooooooo..", "...k.k...k.k..", "...k.k...k.k.."],
            ["....ooooo.....", "..oowwwwwoo...", ".owwwwwwwwwokk", ".owwwwwwwwwkek", "..oWWwwwwWWokk", "...ooooooooo..", "....k.k.k.k...", "....k.k.k.k..."],
            ["....ooooo.....", "..oowwwwwoo...", ".owwwwwwwwwo..", ".owwwwwwwwwo..", "..oWWwwwwWWokk", "...ooooooooekk", "...k.k...k.kk.", "...k.k...k.k.."]],
            Pal("o#8c877c", "w#f3f0e8", "W#d9d4c7", "k#3b3632", "e#f3f0e8")),
        ["cow"] = new([
            [".............hh.", "..oooooooooookk.", ".owwbbwwwwbwokkk", ".owbbbwwwwwwokpk", ".owwbwwwbbwwo...", "..owwwwwwwwo....", "..k.k....k.k....", "..k.k....k.k...."],
            [".............hh.", "..oooooooooookk.", ".owwbbwwwwbwokkk", ".owbbbwwwwwwokpk", ".owwbwwwbbwwo...", "..owwwwwwwwo....", "...k.k..k.k.....", "...k.k..k.k....."],
            ["................", "..ooooooooooo...", ".owwbbwwwwbwo...", ".owbbbwwwwwwohh.", ".owwbwwwbbwwokkk", "..owwwwwwwwokpk.", "..k.k....k.k....", "..k.k....k.k...."]],
            Pal("o#5a4a3c", "w#f1ece2", "b#4a3a2e", "p#e8b4a8", "k#2e2620", "h#d8cfbf")),
        ["deer"] = new([
            ["..........a.a.", "...........a..", "..........bbk.", "..........bb..", ".wbbbbbbbbbb..", "..bBBBBBBBb...", "..k..k...k.k..", "..k..k...k.k..", "..k..k...k.k.."],
            ["..........a.a.", "...........a..", "..........bbk.", "..........bb..", ".wbbbbbbbbbb..", "..bBBBBBBBb...", "...k.k..k.k...", "...k.k..k.k...", "...k.k..k.k..."],
            ["..............", "..............", "..............", "..........a.a.", ".wbbbbbbbbbba.", "..bBBBBBBBbbbk", "..k..k...k.k..", "..k..k...k.k..", "..k..k...k.k.."]],
            Pal("a#d9c7a6", "b#9a6a3c", "B#7a5230", "w#f2ebe0", "k#3a2a1e")),
        ["duck"] = new([
            [".....gg..", ".....ggy.", ".wwwwww..", "WwwwwwW..", ".rrrrrr.."],
            [".....gg..", ".....ggy.", ".wwwwww..", "WwwwwwW..", "rr.rr.rr."]],
            Pal("g#2f6b3f", "y#e9a53a", "w#f2efe8", "W#c9c3b6", "r#9fc7dd")),
        ["bird"] = new([["k.....k", ".k...k.", "..k.k..", "...k..."], [".......", "kkk.kkk", "...k...", "......."]], Pal("k#2a2a2e"))
    };

    // life.js sprites
    public static readonly Grid Pigeon = new([
        ["...gg..", "..gwgy.", ".gggg..", "gGGgg..", "..k.k.."],                  // em
        [".......", "...gg..", ".gggwgy", "gGGgg..", "..k.k.."],                  // bica
        ["g.....g", "gg...gg", ".ggwgg.", "..ggy.."],                             // voo1
        [".......", "ggggggg", ".ggwgg.", "..ggy.."]],                            // voo2
        Pal("g#9a9ba6", "G#6c6d78", "w#5e8c7a", "y#e0a64a", "k#c46b5b"));
    public static readonly Grid Dog = new([
        ["........bb.", "b......bbkb", ".bbbbbbbbbp", "..bBBBBBb..", "..k.k..k.k."],
        ["........bb.", "b......bbkb", ".bbbbbbbbbp", "..bBBBBBb..", "...k.kk.k.."],
        [".......bb..", "......bbkb.", "......bbbp.", ".b..bbbbb..", "bbbbbbbbb..", ".kk...k.k.."]],  // senta
        Pal("b#9a6438", "B#74492a", "w#eadcc4", "k#2a2018", "p#d9747a"));
    public static readonly Grid Fox = new([
        ["..........oo", "w........ook", "wwoooooooooo", ".woOOOOOOo..", "...k.k..k.k."],
        ["..........oo", "w........ook", "wwoooooooooo", ".woOOOOOOo..", "....kk..kk.."]],
        Pal("o#d7732e", "O#a9531b", "w#f4ede2", "k#2a1c14"));
    public static readonly Grid Boat = new([
        [".....mf.....", ".....ms.....", "....sms.....", "...ssmsS....", "..sssmSS....", ".ssssmSSS...", "sssssmSSSS..", ".....m......", "hhhhhhhhhhhh", ".hhhhhhhhhh.", "..HHHHHHHH.."]],
        Pal("h#7a5232", "H#553821", "s#f4efe2", "S#d6d0c0", "m#3a2a1c", "f#d9483b"));
    public static readonly Grid FishGrid = new([["..ss.", "sswws", "..ss."]], Pal("s#9fb7c9", "w#e8f1f6"));

    private static Dictionary<char, string> Pal(params string[] entries) => entries.ToDictionary(e => e[0], e => e[1..]);

    /// <summary>sprite()/animal(): one canvas pixel per grid cell, '.' transparent, optional mirror.</summary>
    public static (byte[] Pixels, int Width, int Height) Render(Grid grid, int frame, bool flip)
    {
        var rows = grid.Frames[frame % grid.Frames.Length];
        int h = rows.Length, w = rows.Max(r => r.Length);
        var px = new byte[w * h * 4];
        for (int j = 0; j < h; j++)
        for (int i = 0; i < rows[j].Length; i++)
        {
            char ch = rows[j][i];
            if (ch == '.') continue;
            var (r, g, b) = Hex(grid.Palette.TryGetValue(ch, out var c) ? c : "#f0f");
            int o = (j * w + (flip ? w - 1 - i : i)) * 4;
            px[o] = r; px[o + 1] = g; px[o + 2] = b; px[o + 3] = 255;
        }
        return (px, w, h);
    }

    /// <summary>life.js balaoImg(cs): 16×24 striped envelope, highlight, ropes and basket.</summary>
    public static byte[] HotAirBalloon(string a, string b)
    {
        const int w = 16, h = 24;
        var px = new byte[w * h * 4];
        void Put(int x, int y, string hex)
        {
            var (r, g, bl) = Hex(hex);
            int o = (y * w + x) * 4;
            px[o] = r; px[o + 1] = g; px[o + 2] = bl; px[o + 3] = 255;
        }
        for (int y = 0; y < 16; y++)
        {
            double ry = (y - 7.5) / 8.2, half = Math.Sqrt(Math.Max(0, 1 - ry * ry)) * 7.6 * (y > 10 ? 1 - (y - 10) * .09 : 1);
            for (int x = 0; x < w; x++)
            {
                double dx = x - 7.5;
                if (Math.Abs(dx) > half) continue;
                int stripe = (int)Math.Floor((dx + 8) / 2.7) % 2;
                Put(x, y, stripe != 0 ? b : a);
            }
        }
        // fillStyle 'rgba(255,255,255,.35)', fillRect(4,3,2,4): source-over on the envelope
        for (int y = 3; y < 7; y++)
        for (int x = 4; x < 6; x++)
        {
            int o = (y * w + x) * 4;
            for (int c = 0; c < 3; c++) px[o + c] = (byte)Math.Round(px[o + c] * (1 - .35) + 255 * .35, MidpointRounding.ToEven);
            px[o + 3] = 255;
        }
        foreach (var (x, y) in new[] { (5, 16), (10, 16), (5, 17), (10, 17), (6, 18), (9, 18) }) Put(x, y, "#4a3424");
        for (int y = 19; y < 22; y++) for (int x = 6; x < 10; x++) Put(x, y, "#8a5a34");
        for (int x = 6; x < 10; x++) Put(x, 21, "#6b4426");
        return px;
    }

    /// <summary>life.js brilhoImg(cor): 64×64 radial glow, alpha 1 → .55 (at .25) → 0, colour <paramref name="rgb"/>.</summary>
    public static byte[] Glow(byte r, byte g, byte b)
    {
        const int n = 64;
        var px = new byte[n * n * 4];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            double d = Math.Sqrt((x + .5 - 32) * (x + .5 - 32) + (y + .5 - 32) * (y + .5 - 32)) / 32;
            double a = d >= 1 ? 0 : d < .25 ? 1 - (1 - .55) * d / .25 : .55 * (1 - (d - .25) / .75);
            int o = (y * n + x) * 4;
            px[o] = r; px[o + 1] = g; px[o + 2] = b; px[o + 3] = (byte)Math.Round(a * 255);
        }
        return px;
    }

    /// <summary>life.js nuvemImg(v, branca): 200×120, nine radial puffs (alpha .55 → 0) from mulberry(9173+v·31).</summary>
    public static byte[] Cloud(int variant, bool white)
    {
        const int w = 200, h = 120;
        var rng = LegacyJsMath.Rng(9173 + variant * 31);
        var keep = new double[w * h];
        Array.Fill(keep, 1.0);
        for (int i = 0; i < 9; i++)
        {
            double x = 40 + rng() * 120, y = 35 + rng() * 50, r = 22 + rng() * 30;
            int x0 = Math.Max(0, (int)Math.Floor(x - r)), x1 = Math.Min(w, (int)Math.Ceiling(x + r));
            int y0 = Math.Max(0, (int)Math.Floor(y - r)), y1 = Math.Min(h, (int)Math.Ceiling(y + r));
            for (int py = y0; py < y1; py++)
            for (int ppx = x0; ppx < x1; ppx++)
            {
                double d = Math.Sqrt((ppx + .5 - x) * (ppx + .5 - x) + (py + .5 - y) * (py + .5 - y)) / r;
                if (d < 1) keep[py * w + ppx] *= 1 - .55 * (1 - d);
            }
        }
        byte c = white ? (byte)255 : (byte)0;
        var px = new byte[w * h * 4];
        for (int i = 0; i < w * h; i++)
        {
            px[i * 4] = c; px[i * 4 + 1] = c; px[i * 4 + 2] = c;
            px[i * 4 + 3] = (byte)Math.Round((1 - keep[i]) * 255);
        }
        return px;
    }

    public static (byte R, byte G, byte B) Hex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 3) hex = string.Concat(hex.Select(c => new string(c, 2)));
        return (Convert.ToByte(hex[..2], 16), Convert.ToByte(hex[2..4], 16), Convert.ToByte(hex[4..6], 16));
    }
}
