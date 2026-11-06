using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// CST-241: the look of the text readers (plain text, a PDF's text layer): size, line spacing and page colours, one
/// preference for the reader kind (<see cref="ReaderKinds.Text"/>) kept in the private reading state, never in the
/// editor's preferences. Without a saved one each view keeps its own built-in look.
/// </summary>
public sealed record TextReaderLook(double FontSize, double LineHeight, string Theme)
{
    public static TextReaderLook From(ReaderPreferences? saved, double fontSize, double lineHeight) => saved is null
        ? new(fontSize, lineHeight, "system")
        : new(Math.Clamp(saved.FontSize, 10, 48), Math.Clamp(saved.LineHeight, 1, 3), saved.Theme);

    public ReaderPreferences ToPreferences() => new(FontSize, LineHeight, Theme: Theme);
    public double LinePixels => Math.Round(FontSize * LineHeight);

    /// <summary>The menu: each entry says what it changes to (the same steps as the EPUB reader's "Aa").</summary>
    public IReadOnlyList<(string Label, TextReaderLook Next)> Choices()
    {
        double spacing = LineHeight >= 2 ? 1.2 : Math.Round(LineHeight + .2, 1);
        string theme = Theme switch { "system" => "light", "light" => "sepia", "sepia" => "dark", _ => "system" };
        return
        [
            ("Letra maior", this with { FontSize = Math.Min(48, FontSize + 2) }),
            ("Letra menor", this with { FontSize = Math.Max(10, FontSize - 2) }),
            ("Entrelinha: " + spacing.ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")), this with { LineHeight = spacing }),
            ("Tema: " + theme switch { "light" => "claro", "sepia" => "sépia", "dark" => "escuro", _ => "do sistema" }, this with { Theme = theme })
        ];
    }

    /// <summary>The page's colours, or null to follow the app's theme (the same colours as the EPUB reader).</summary>
    public (IBrush Background, IBrush Foreground)? Colors => Theme switch
    {
        "dark" => (UrbeTheme.Brush("#171b21"), UrbeTheme.Brush("#e4e4e7")),
        "sepia" => (UrbeTheme.Brush("#f3e7ce"), UrbeTheme.Brush("#45382a")),
        "light" => (UrbeTheme.Brush("#fdfcf9"), UrbeTheme.Brush("#272b31")),
        _ => null
    };
}
