namespace Urbe.Core;

/// <summary>
/// Procedural ground details from the 1.8.4-beta pixel-art painter.
/// Operates on a caller-owned square RGBA pixel buffer. No renderer, browser,
/// vault, provider, randomness or filesystem required.
/// </summary>
public static class LegacyWorldGroundDecor
{
    private static readonly string[] Names =
    [
        "flowers", "rock", "boulder", "pebbles", "bush",
        "tallgrass", "drygrass", "fern", "mushroom", "log", "stump",
        "reeds", "puddle", "lily", "shorerock", "drybush",
        "driftwood", "snowpatch", "tuft", "shell"
    ];

    public static IReadOnlyList<string> Kinds { get; } = Array.AsReadOnly(Names);

    /// <summary>
    /// Paints a decoration at (pixelX,pixelY) into a square RGBA image.
    /// Origin and world coordinates are deliberately different: the former
    /// is an output tile position, while the latter seeds the old JS hash.
    /// </summary>
    public static void Paint(
        byte[] pixels, int side, int pixelX, int pixelY,
        string kind, int worldX, int worldY, int slot = 0)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentNullException.ThrowIfNull(kind);
        if (side <= 0 || (long)side * side * 4 != pixels.Length)
            throw new ArgumentException("Imagem RGBA quadrada inválida.", nameof(pixels));
        if (Array.IndexOf(Names, kind) < 0)
            throw new ArgumentException("Detalhe de terreno desconhecido.", nameof(kind));

        var painter = new Painter(pixels, side);
        var ox = pixelX;
        var oy = pixelY;
        double R(int seed) =>
            LegacyWorldPixelTextures.Hash(worldX, worldY, unchecked(seed + slot * 50));

        switch (kind)
        {
            case "flowers":
            {
                string[] flowers = ["#f2d34f", "#e8e8f0", "#d9687a", "#9b7fe0", "#f29a4f"];
                var count = 5 + Floor(R(40) * 5);
                var baseColor = flowers[Floor(R(41) * flowers.Length)];
                for (var i = 0; i < count; i++)
                {
                    var x = ox + 1 + Floor(R(i + 1) * 14);
                    var y = oy + 1 + Floor(R(i + 9) * 13);
                    var color = R(i + 30) < 0.7
                        ? baseColor
                        : flowers[Floor(R(i + 30) * flowers.Length)];
                    painter.Put(x, y, color);
                    painter.Put(x, y + 1, "#4f7a34");
                    if (R(i + 60) < 0.4)
                    {
                        painter.Put(x + 1, y, color);
                        painter.Put(x - 1, y, color);
                    }
                }
                break;
            }
            case "rock":
            {
                var x0 = ox + 4 + Floor(R(2) * 6);
                var y0 = oy + 5 + Floor(R(3) * 6);
                for (var y = 0; y < 4; y++)
                for (var x = 0; x < 5; x++)
                {
                    if ((x is 0 or 4) && (y is 0 or 3))
                        continue;
                    painter.Put(x0 + x, y0 + y,
                        y == 0 ? "#b3aea8" :
                        y == 3 || x == 4 ? "#6c6761" : "#8f8a84");
                }
                painter.Dim(x0 + 5, y0 + 3, .75);
                painter.Dim(x0 + 2, y0 + 4, .75);
                painter.Dim(x0 + 3, y0 + 4, .75);
                break;
            }
            case "boulder":
                painter.Blob(ox + 8, oy + 8, 5, 4,
                    ["#6a645e", "#8c867f", "#b0aaa3"], true);
                break;
            case "pebbles":
                for (var i = 0; i < 3; i++)
                {
                    var x = ox + 2 + Floor(R(i + 3) * 12);
                    var y = oy + 2 + Floor(R(i + 7) * 12);
                    painter.Put(x, y, "#a39d94");
                    painter.Put(x + 1, y, "#8a847c");
                    painter.Dim(x + 1, y + 1, .8);
                }
                break;
            case "bush":
            {
                var x = ox + 4 + Floor(R(4) * 8);
                var y = oy + 5 + Floor(R(5) * 6);
                var variance = R(6);
                painter.Blob(x, y,
                    4 + (variance < .5 ? 1 : 0),
                    3 + (variance < .3 ? 1 : 0),
                    variance < .5
                        ? ["#35592a", "#4d7b36", "#6a9a48"]
                        : ["#3c5f2c", "#557f38", "#79a54e"], true);
                if (variance > .75)
                {
                    painter.Put(x - 1, y - 1, "#d9687a");
                    painter.Put(x + 2, y, "#d9687a");
                }
                break;
            }
            case "tallgrass":
            case "drygrass":
            {
                var colors = kind == "drygrass"
                    ? new[] { "#b39b52", "#cdb766", "#8f7b3f" }
                    : ["#5d8a3c", "#7aa850", "#4a7331"];
                for (var i = 0; i < 5; i++)
                {
                    var x = ox + 1 + Floor(R(i + 11) * 14);
                    var y = oy + 3 + Floor(R(i + 17) * 11);
                    var height = 2 + Floor(R(i + 23) * 2);
                    for (var k = 0; k < height; k++)
                        painter.Put(
                            x + (k == height - 1 && R(i + 29) < .5 ? 1 : 0),
                            y - k, colors[k == height - 1 ? 1 : i % 3 == 0 ? 2 : 0]);
                }
                break;
            }
            case "fern":
            {
                var x = ox + 4 + Floor(R(8) * 8);
                var y = oy + 6 + Floor(R(9) * 6);
                for (var i = -3; i <= 3; i++)
                {
                    painter.Put(x + i, (int)(y - Math.Abs(i) * .5), "#44773a");
                    if (Math.Abs(i) < 3)
                        painter.Put(x + i, y + 1, "#2f5a2a");
                }
                painter.Put(x, y - 2, "#44773a");
                break;
            }
            case "mushroom":
                for (var i = 0; i < 1 + Floor(R(12) * 2); i++)
                {
                    var x = ox + 3 + Floor(R(i + 13) * 10);
                    var y = oy + 4 + Floor(R(i + 14) * 9);
                    var red = R(15) < .6;
                    painter.Put(x, y + 1, "#efe6d6");
                    painter.Put(x - 1, y, red ? "#c94a3a" : "#a6784a");
                    painter.Put(x, y, red ? "#e05a47" : "#bf8c58");
                    painter.Put(x + 1, y, red ? "#c94a3a" : "#a6784a");
                    if (red)
                        painter.Put(x, y, "#f4efe6");
                }
                break;
            case "log":
            {
                var x = ox + 2 + Floor(R(16) * 4);
                var y = oy + 6 + Floor(R(17) * 5);
                for (var i = 0; i < 10; i++)
                {
                    painter.Put(x + i, y, "#8a6340");
                    painter.Put(x + i, y + 1, "#6b4a2f");
                    painter.Dim(x + i + 1, y + 2, .75);
                }
                painter.Put(x + 9, y, "#c9a77a");
                painter.Put(x + 9, y + 1, "#a88560");
                if (R(18) < .5)
                {
                    painter.Put(x + 4, y - 1, "#5a8a3c");
                    painter.Put(x + 5, y - 1, "#6a9a48");
                }
                break;
            }
            case "stump":
            {
                var x = ox + 6 + Floor(R(19) * 4);
                var y = oy + 7 + Floor(R(20) * 4);
                painter.Put(x, y, "#c9a77a");
                painter.Put(x + 1, y, "#b8946a");
                painter.Put(x, y + 1, "#6b4a2f");
                painter.Put(x + 1, y + 1, "#5a3e27");
                painter.Dim(x + 2, y + 1, .75);
                break;
            }
            case "reeds":
                for (var i = 0; i < 4; i++)
                {
                    var x = ox + 2 + Floor(R(i + 4) * 12);
                    var y = oy + 4 + Floor(R(i + 7) * 8);
                    for (var h = 0; h < 4; h++)
                        painter.Put(x, y + h, "#6f7d3f");
                    painter.Put(x, y - 1, "#8c7a45");
                    painter.Put(x, y - 2, "#8c7a45");
                }
                break;
            case "puddle":
                painter.Blob(ox + 8, oy + 8, 4, 2,
                    ["#3d6f8f", "#4f86a8", "#7fb2cf"], false);
                break;
            case "lily":
                for (var i = 0; i < 1 + Floor(R(21) * 3); i++)
                {
                    var x = ox + 3 + Floor(R(i + 22) * 10);
                    var y = oy + 3 + Floor(R(i + 25) * 10);
                    painter.Blob(x, y, 2, 1,
                        ["#3f7a36", "#5a9a48", "#79b85e"], false);
                    if (R(i + 28) < .4)
                        painter.Put(x, y, "#f2a8c2");
                }
                break;
            case "shorerock":
                painter.Blob(ox + 8, oy + 8, 3, 2,
                    ["#5f5a55", "#7d7770", "#a39c94"], false);
                break;
            case "drybush":
            {
                var x = ox + 5 + Floor(R(31) * 6);
                var y = oy + 7 + Floor(R(32) * 4);
                for (var i = -2; i <= 2; i++)
                {
                    painter.Put(x + i, y, "#7a6a42");
                    painter.Put(x + i, y - 1 - (i & 1), "#9a8752");
                }
                painter.Put(x, y - 3, "#9a8752");
                painter.Dim(x + 1, y + 1, .8);
                painter.Dim(x + 2, y + 1, .8);
                break;
            }
            case "driftwood":
            {
                var x = ox + 3 + Floor(R(33) * 5);
                var y = oy + 8 + Floor(R(34) * 4);
                for (var i = 0; i < 7; i++)
                    painter.Put(x + i, y + (i > 4 ? 1 : 0),
                        i % 3 != 0 ? "#b39b7a" : "#9c8466");
                break;
            }
            case "snowpatch":
                painter.Blob(
                    ox + 6 + Floor(R(35) * 5),
                    oy + 6 + Floor(R(36) * 5), 3, 2,
                    ["#d9e2e8", "#e9eff3", "#f7fafc"], false);
                break;
            case "tuft":
                for (var i = 0; i < 3; i++)
                {
                    var x = ox + 3 + Floor(R(i + 2) * 10);
                    var y = oy + 4 + Floor(R(i + 5) * 9);
                    painter.Put(x, y, "#8d8f4f");
                    painter.Put(x - 1, y + 1, "#8d8f4f");
                    painter.Put(x + 1, y + 1, "#8d8f4f");
                }
                break;
            case "shell":
                painter.Put(ox + 7, oy + 8, "#f4e9dc");
                painter.Put(ox + 8, oy + 8, "#e7c9b5");
                break;
        }
    }

    private static int Floor(double number) => (int)Math.Floor(number);

    private sealed class Painter(byte[] pixels, int side)
    {
        public void Put(int x, int y, string hex)
        {
            if ((uint)x >= (uint)side || (uint)y >= (uint)side)
                return;
            var offset = (y * side + x) * 4;
            // Fixed palette: decode hex without per-pixel Substring allocations.
            pixels[offset] = HexByte(hex, 1);
            pixels[offset + 1] = HexByte(hex, 3);
            pixels[offset + 2] = HexByte(hex, 5);
            pixels[offset + 3] = 255;
        }

        private static byte HexByte(string hex, int index)
        {
            static int Nibble(char c) =>
                c <= '9' ? c - '0' : (c | (char)0x20) - 'a' + 10;
            return (byte)((Nibble(hex[index]) << 4) | Nibble(hex[index + 1]));
        }

        public void Dim(int x, int y, double factor)
        {
            if ((uint)x >= (uint)side || (uint)y >= (uint)side)
                return;
            var offset = (y * side + x) * 4;
            for (var channel = 0; channel < 3; channel++)
            {
                // Uint8ClampedArray in JS uses round-half-to-even.
                pixels[offset + channel] = (byte)Math.Clamp(
                    Math.Round(pixels[offset + channel] * factor,
                        MidpointRounding.ToEven), 0, 255);
            }
        }

        public void Blob(
            int cx, int cy, int rx, int ry,
            string[] colors, bool shadow)
        {
            if (shadow)
                for (var y = -ry; y <= ry; y++)
                for (var x = -rx; x <= rx; x++)
                    if ((double)x * x / (rx * rx) + (double)y * y / (ry * ry) <= 1)
                        Dim(cx + x + 1, cy + y + 1, .78);

            for (var y = -ry; y <= ry; y++)
            for (var x = -rx; x <= rx; x++)
            {
                var distance = (double)x * x / (rx * rx) + (double)y * y / (ry * ry);
                if (distance > 1)
                    continue;
                var light = -(double)x / rx - (double)y / ry;
                var color = light > .55 ? colors[2] :
                    light < -.5 || (distance > .8 && y > 0)
                        ? colors[0]
                        : colors[1];
                Put(cx + x, cy + y, color);
            }
        }
    }
}
