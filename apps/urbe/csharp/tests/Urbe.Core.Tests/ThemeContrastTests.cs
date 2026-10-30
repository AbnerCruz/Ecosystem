using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// CST-105: in every ready-made theme, the three text colours read at WCAG AA (4.5:1) on every surface. texto3
/// (hints, dates, counts) was 2.5–3.9:1 in all of them; it moved the least it could toward texto2, keeping each tint.
/// </summary>
public sealed class ThemeContrastTests
{
    private static double Luminance(string hex)
    {
        double C(int i) { var c = Convert.ToInt32(hex.Substring(1 + 2 * i, 2), 16) / 255.0; return c <= .03928 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4); }
        return .2126 * C(0) + .7152 * C(1) + .0722 * C(2);
    }

    private static double Contrast(string a, string b)
    {
        var (x, y) = (Luminance(a), Luminance(b));
        return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05);
    }

    public static TheoryData<string> Themes()
    {
        var data = new TheoryData<string>();
        foreach (var p in LegacyCustomizeTheme.Presets) data.Add(p.Id);
        return data;
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void EveryTextColourReadsOnEverySurface(string id)
    {
        var colors = LegacyCustomizeTheme.PresetById(id)!.Colors;
        foreach (var text in new[] { "texto", "texto2", "texto3" })
            foreach (var surface in new[] { "fundo", "superficie", "superficie2", "superficie3" })
                Assert.True(Contrast(colors[text], colors[surface]) >= 4.5,
                    $"{id}: {text} {colors[text]} sobre {surface} {colors[surface]} = {Contrast(colors[text], colors[surface]):0.00}:1");
    }
}
