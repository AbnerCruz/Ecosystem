namespace Urbe.Core;

/// <summary>
/// Medieval 48x56 construction sprites ported from the original 1.8.4-beta
/// house/roofRows/window2 functions. Pure in-memory output for native hosts.
/// The legacy polygon antialiasing difference remains a visual acceptance gate.
/// </summary>
public static class LegacyWorldBuildings
{
    public const int Width = 48;
    public const int Height = 56;

    private static readonly string[] Kinds =
        ["house", "tower", "hall", "workshop", "market", "store", "dyer"];

    private sealed record Style(
        string Wall, string ShadowWall, string Beam,
        string[][] Roofs, string Base, bool Snow = false, bool Flat = false);

    private static readonly Dictionary<string, Style> Styles =
        new(StringComparer.Ordinal)
        {
            ["temperate"] = new(
                "#e8dcc0", "#cbbb98", "#5b3b24",
                [
                    ["#b4553b", "#8f3f2c", "#cf6d4f"],
                    ["#c9a24e", "#a8843a", "#dfbb67"],
                    ["#6f5a4c", "#56453a", "#86705f"]
                ], "#8d857c"),
            ["cold"] = new(
                "#9a8f86", "#7d736b", "#4d3a2c",
                [
                    ["#56626e", "#434d57", "#6a7784"],
                    ["#5f4b3d", "#4a3a2f", "#766050"]
                ], "#6f6962", Snow: true),
            ["dry"] = new(
                "#d9b98a", "#bf9c6c", "#8a6038",
                [
                    ["#c7703f", "#a3572f", "#dc8a55"],
                    ["#b8864f", "#98693a", "#cf9e63"]
                ], "#b79d77", Flat: true),
            ["coast"] = new(
                "#e6e1d3", "#c9c2b0", "#4d6b86",
                [
                    ["#4f7aa0", "#3c6284", "#6591b6"],
                    ["#b4553b", "#8f3f2c", "#cf6d4f"]
                ], "#a09a8e"),
            ["wet"] = new(
                "#b59a74", "#977d5a", "#4e3a26",
                [
                    ["#8f8a4a", "#6f6a36", "#a7a35c"]
                ], "#6f6a5e")
        };

    public static IReadOnlyList<string> BuildingKinds { get; } = Array.AsReadOnly(Kinds);

    public static string StyleForBiome(string biome) => biome switch
    {
        "taiga" or "tundra" or "snow" or "mountain" or "peak" => "cold",
        "desert" or "savanna" or "steppe" => "dry",
        "beach" => "coast",
        "swamp" => "wet",
        _ => "temperate"
    };

    /// <summary>
    /// Uses canonical legacy style/kind and variant; no host or filesystem.
    /// Caller can EncodePng(output, Width, Height) for native display.
    /// </summary>
    public static byte[] CreateBuilding(string kind, string style,
        int variant = 0, bool flowers = false)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(style);
        if (Array.IndexOf(Kinds, kind) < 0)
            throw new ArgumentException("Tipo de construção desconhecido.", nameof(kind));
        if (!Styles.TryGetValue(style, out var st))
            throw new ArgumentException("Estilo arquitetônico desconhecido.", nameof(style));
        if (variant < 0 || variant > 255)
            throw new ArgumentOutOfRangeException(nameof(variant));

        var sp = new PixelCanvas(Width, Height);
        var roof = st.Roofs[variant % st.Roofs.Length];
        var seed = variant * 7 + kind.Length;
        sp.Shadow(24, 52, 22, 3.2);

        if (kind == "tower")
        {
            sp.Rect(12, 20, 24, 33, "#8f877e");
            for (var y = 21; y < 52; y += 4)
                for (var x = 12 + ((y >> 2) % 2) * 3; x < 36; x += 6)
                    sp.Rect(x, y, 5, 3, "#a39b91");
            sp.Rect(33, 20, 3, 33, "#756d65");
            sp.Rect(10, 14, 28, 7, "#7c746b");
            for (var x = 10; x < 38; x += 5)
                sp.Rect(x, 10, 3, 4, "#7c746b");
            sp.Rect(21, 40, 6, 12, "#4a3322");
            sp.Rect(22, 41, 4, 11, "#6b4a2e");
            Window(sp, 22, 27, "#4d3a2c");
            sp.Polygon([24, 2, 33, 14, 15, 14], roof[0]);
            sp.Polygon([24, 2, 24, 14, 15, 14], roof[2]);
            if (st.Snow)
                sp.Polygon([24, 2, 28, 7, 20, 7], "#f2f5f8");
            return sp.Pixels;
        }

        var wl = kind == "hall" ? 3 : 5;
        var wr = kind == "hall" ? 45 : 43;
        const int wt = 26, wb = 48;

        sp.Rect(wl - 1, wb, wr - wl + 3, 4, st.Base);
        for (var x = wl; x < wr; x += 5)
            sp.Rect(x, wb + 1, 4, 2, "#a7a097");
        sp.Rect(wl, wt, wr - wl + 1, wb - wt, st.Wall);
        sp.Rect(wr - 6, wt, 7, wb - wt, st.ShadowWall);

        if (!st.Flat)
        {
            sp.Rect(wl, wt, wr - wl + 1, 2, st.Beam);
            sp.Rect(wl, wt + 11, wr - wl + 1, 1, st.Beam);
            sp.Rect(wl, wb - 1, wr - wl + 1, 1, st.Beam);
            foreach (var bx in new[] { wl, wl + 11, wr - 11, wr })
                sp.Rect(bx, wt, 2, wb - wt, st.Beam);
            for (var i = 0; i < 9; i++)
            {
                sp.Point(wl + 2 + i, wt + 2 + i, st.Beam);
                sp.Point(wr - 2 - i, wt + 2 + i, st.Beam);
            }
        }
        else
        {
            sp.Rect(wl, wt, wr - wl + 1, 1, st.ShadowWall);
            for (var i = 0; i < 5; i++)
                sp.Point(wl + 4 + i * 7, wt + 4 + i % 2 * 6, st.ShadowWall);
        }

        const int door = 21;
        sp.Rect(door - 1, 36, 8, 12, st.Beam);
        sp.Rect(door, 38, 6, 10, "#6b4a2e");
        sp.Rect(door + 1, 37, 4, 1, "#6b4a2e");
        sp.Rect(door + 3, 38, 1, 10, "#553a24");
        sp.Point(door + 4, 43, "#d9b44a");
        Window(sp, wl + 4, 31, st.Beam);
        Window(sp, wr - 9, 31, st.Beam);
        if (kind == "hall")
        {
            Window(sp, wl + 13, 31, st.Beam);
            Window(sp, wr - 18, 31, st.Beam);
        }

        if (st.Flat)
        {
            sp.Rect(wl - 2, wt - 5, wr - wl + 5, 5, roof[0]);
            sp.Rect(wl - 2, wt - 1, wr - wl + 5, 1, roof[1]);
            sp.Rect(wl - 2, wt - 5, wr - wl + 5, 1, roof[2]);
            for (var x = wl; x < wr; x += 6)
                sp.Rect(x, wt - 7, 3, 2, roof[1]);
        }
        else
        {
            RoofRows(sp, wl - 4, 6, wr + 4, wt + 1, roof, wl + 7, wr - 7);
            sp.Rect(wr - 12, 1, 5, 10, "#8d857c");
            sp.Rect(wr - 12, 1, 5, 1, "#6f6962");
            sp.Rect(wr - 8, 1, 1, 10, "#6f6962");
            if (kind == "workshop")
                sp.Disc(wr - 9, -2, 2.5, 2.5, "#c9c9c9", .55);

            if (st.Snow)
            {
                for (var x = wl - 3; x <= wr + 3; x++)
                    if (Hash(x, seed, 4) < .75)
                        sp.Rect(x, wt - 1 - (int)Math.Floor(Hash(x, seed, 5) * 2),
                            1, 2, "#f2f5f8");
                sp.Rect(wl + 7, 6, wr - wl - 13, 2, "#f2f5f8");
            }
        }

        if (kind == "workshop")
        {
            sp.Rect(wr + 1, 40, 3, 8, "#5b5550");
            sp.Rect(wr, 39, 5, 2, "#77706a");
        }
        if (kind == "market")
        {
            for (var x = wl - 3; x <= wr + 3; x += 4)
                sp.Rect(x, wt - 2, 2, 5,
                    x % 8 != 0 ? "#c9453a" : "#efe6d2");
            sp.Rect(wl + 2, wb - 6, 8, 4, "#a7743f");
            sp.Rect(wr - 10, wb - 6, 8, 4, "#a7743f");
            sp.Rect(wl + 3, wb - 8, 3, 2, "#d94f3a");
            sp.Rect(wr - 8, wb - 8, 3, 2, "#e0b43d");
        }
        if (kind == "hall")
        {
            sp.Rect(23, 8, 1, 10, "#4d3a2c");
            sp.Rect(24, 8, 6, 5, "#7d5ab4");
            sp.Rect(24, 12, 6, 1, "#5f4290");
        }
        if (kind == "store")
        {
            sp.Rect(wl + 1, wb - 5, 5, 5, "#9a6b3e");
            sp.Rect(wl + 1, wb - 5, 5, 1, "#b98653");
            sp.Rect(wr - 5, wb - 5, 5, 5, "#9a6b3e");
            sp.Rect(wr - 5, wb - 5, 5, 1, "#b98653");
        }
        if (kind == "dyer")
        {
            sp.Rect(wl - 2, wb - 12, 1, 10, "#5b3b24");
            sp.Rect(wl - 1, wb - 12, 3, 6, "#3f7ac0");
            sp.Rect(wr + 1, wb - 12, 1, 10, "#5b3b24");
            sp.Rect(wr - 1, wb - 12, 2, 6, "#c0493f");
        }
        if (flowers)
        {
            for (var x = wl + 3; x < wl + 10; x++)
                sp.Point(x, 37, Hash(x, seed, 8) < .5 ? "#d9687a" : "#f2d34f");
            for (var x = wr - 8; x < wr - 1; x++)
                sp.Point(x, 37, Hash(x, seed, 9) < .5 ? "#9b7fe0" : "#f2d34f");
        }
        return sp.Pixels;
    }

    private static double Hash(int x, int y, int seed) =>
        LegacyWorldPixelTextures.Hash(x, y, seed);

    private static int Round(double value) => (int)Math.Floor(value + .5);

    private static void Window(PixelCanvas sp, int x, int y, string frame)
    {
        sp.Rect(x - 1, y - 1, 6, 6, frame);
        sp.Rect(x, y, 4, 4, "#f3c865");
        sp.Rect(x, y, 4, 1, "#fbe39a");
        sp.Rect(x + 2, y, 1, 4, frame);
        sp.Rect(x, y + 2, 4, 1, frame);
    }

    private static void RoofRows(PixelCanvas sp,
        int x0, int y0, int x1, int y1, string[] colors, int ridgeL, int ridgeR)
    {
        for (var y = y0; y <= y1; y++)
        {
            var t = (double)(y - y0) / (y1 - y0);
            var l = Round(ridgeL - (ridgeL - x0) * t);
            var r = Round(ridgeR + (x1 - ridgeR) * t);
            sp.Rect(l, y, r - l + 1, 1,
                (y - y0) % 3 == 2 ? colors[1] : colors[0]);
            if ((y - y0) % 3 == 1)
                for (var x = l + ((y >> 1) % 2 != 0 ? 1 : 3); x < r; x += 4)
                    sp.Point(x, y, colors[1]);
        }
        sp.Rect(ridgeL, y0, ridgeR - ridgeL + 1, 1, colors[2]);
        sp.Rect(x0, y1, x1 - x0 + 1, 1, colors[1]);
    }
}
