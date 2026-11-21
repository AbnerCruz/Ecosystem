namespace Urbe.Core;

/// <summary>
/// A finished look for a whole video (#956, owner 19/11/2026: "o que ficou pronto do vídeo tava simplesmente ruim"):
/// a background gradient, the colour of text, two colours for diagrams, and the subtitle box. Elements and scenes that
/// keep their default colour take the theme's; a colour chosen by hand is kept. Colours are #RRGGBB; the subtitle box
/// carries its transparency first, #AARRGGBB.
/// </summary>
public sealed record VideoTheme(string Id, string Label, string Top, string Bottom, string Text, string Accent, string Secondary,
    string SubtitleBox, string SubtitleText)
{
    /// <summary>The colour an element of <paramref name="kind"/> takes when it keeps the default (#ffffff).</summary>
    public string ColorFor(string kind) => kind switch
    {
        "path" => Accent,
        "shape" or "plot" => Secondary,
        _ => Text
    };
}

public static class VideoThemes
{
    /// <summary>The defaults a theme replaces: the element colour and the scene background a new one gets.</summary>
    public const string DefaultColor = "#ffffff", DefaultBackground = "#101418";

    public static readonly IReadOnlyList<VideoTheme> All =
    [
        new("noite", "Noite", "#0b1d33", "#122b4d", "#f5f7fb", "#ffd166", "#4cc9f0", "#b3000000", "#ffffff"),
        new("quadro", "Quadro-negro", "#1e3a2b", "#142a1f", "#f1f1e6", "#f6c453", "#9ad1a8", "#a6000000", "#ffffff"),
        new("papel", "Papel", "#f8f4eb", "#ebe3d3", "#1d2433", "#d1495b", "#2e86ab", "#d91d2433", "#ffffff"),
        new("neon", "Neon", "#0d0221", "#2a1050", "#ffffff", "#ff2e88", "#2de2e6", "#b3000000", "#ffffff"),
        new("sobrio", "Sóbrio", "#121418", "#1c2129", "#e8eaed", "#8ab4f8", "#81c995", "#aa000000", "#ffffff"),
    ];

    public static VideoTheme? Find(string? id) => id is null ? null : All.FirstOrDefault(t => t.Id == id);
}
