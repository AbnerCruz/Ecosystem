using System.Globalization;

namespace Lunet.Studio;

/// <summary>ARGB color helpers for the Studios: HSV for pickers, hex for input, palette extraction.</summary>
public static class SpriteColor
{
    public static uint FromHsv(float hue, float saturation, float value, int alpha)
    {
        hue = ((hue % 360) + 360) % 360;
        saturation = Math.Clamp(saturation, 0, 1);
        value = Math.Clamp(value, 0, 1);
        float c = value * saturation, x = c * (1 - Math.Abs(hue / 60 % 2 - 1)), m = value - c;
        var (r, g, b) = (int)(hue / 60) switch
        {
            0 => (c, x, 0f), 1 => (x, c, 0f), 2 => (0f, c, x), 3 => (0f, x, c), 4 => (x, 0f, c), _ => (c, 0f, x),
        };
        uint R = (uint)Math.Round((r + m) * 255), G = (uint)Math.Round((g + m) * 255), B = (uint)Math.Round((b + m) * 255);
        return (uint)Math.Clamp(alpha, 0, 255) << 24 | R << 16 | G << 8 | B;
    }

    public static (float Hue, float Saturation, float Value, int Alpha) ToHsv(uint argb)
    {
        float r = (argb >> 16 & 0xff) / 255f, g = (argb >> 8 & 0xff) / 255f, b = (argb & 0xff) / 255f;
        float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), delta = max - min;
        float hue = delta == 0 ? 0 : max == r ? 60 * ((g - b) / delta % 6) : max == g ? 60 * ((b - r) / delta + 2) : 60 * ((r - g) / delta + 4);
        if (hue < 0) hue += 360;
        return (hue, max == 0 ? 0 : delta / max, max, (int)(argb >> 24));
    }

    public static uint? ParseHex(string? text)
    {
        var hex = (text ?? "").Trim().TrimStart('#');
        if (hex.Length is not (6 or 8) || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            return null;
        return hex.Length == 6 ? 0xff000000u | value : value;
    }

    public static string ToHex(uint argb) => (argb >> 24) == 0xff ? $"#{argb & 0xffffff:X6}" : $"#{argb:X8}";

    /// <summary>Distinct opaque/translucent colors of the document, most used first.</summary>
    public static IReadOnlyList<uint> Extract(SpriteDocument document, int max) =>
        document.Frames.SelectMany(f => f).Where(c => c >> 24 != 0)
            .GroupBy(c => c).OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Take(max).Select(g => g.Key).ToArray();
}
