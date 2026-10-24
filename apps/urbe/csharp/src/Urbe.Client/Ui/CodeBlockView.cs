using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Urbe.Client.Ui;

/// <summary>Any language, including plain text. Copy uses the current source, never rendered labels or fences.</summary>
public sealed class CodeBlockView : Border
{
    public SelectableTextBlock TextView { get; }
    public Button CopyButton { get; }
    public Button WrapButton { get; }
    public string SourceText => TextView.Text ?? "";

    public CodeBlockView(string source, string language = "", double size = 13)
    {
        Padding = new Thickness(12, 4, 12, 12);
        CornerRadius = new CornerRadius(UrbeTokens.Radius.M);
        Background = UrbeTheme.Brush(UrbeTheme.Surface);
        TextView = new SelectableTextBlock { Text = source, FontFamily = UrbeTheme.MonoFont,
            FontSize = size, Foreground = UrbeTheme.Brush(UrbeTheme.Text), TextWrapping = TextWrapping.NoWrap };
        TextView.Classes.Add(MarkdownReader.TextClass);
        TextActions.Attach(TextView);
        _scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = TextView };
        CopyButton = ActionButton("Copiar");
        Avalonia.Automation.AutomationProperties.SetName(CopyButton, "Copiar bloco de código");
        CopyButton.Click += async (_, _) =>
        {
            CopyButton.Content = await TextActions.CopyAsync(this, SourceText) ? "Copiado" : "Falha ao copiar";
        };
        WrapButton = ActionButton("Quebrar linhas");
        WrapButton.Click += (_, _) =>
        {
            bool wrap = TextView.TextWrapping == TextWrapping.NoWrap;
            TextView.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
            _scroll.HorizontalScrollBarVisibility = wrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
            WrapButton.Content = wrap ? "Linha inteira" : "Quebrar linhas";
        };
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right,
            Children = { WrapButton, CopyButton } };
        var header = new DockPanel();
        DockPanel.SetDock(actions, Dock.Right);
        header.Children.Add(actions);
        header.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(language) ? "Texto" : language,
            Foreground = UrbeTheme.Brush(UrbeTheme.Text2), FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center });
        Child = new StackPanel { Spacing = 4, Children = { header, _scroll } };
    }

    private readonly ScrollViewer _scroll;
    private static Button ActionButton(string name) => new() { Content = name, FontSize = 11,
        MinHeight = UrbeTokens.MinTouch, Padding = new Thickness(8, 4),
        Background = Brushes.Transparent, Foreground = UrbeTheme.Brush(UrbeTheme.Accent), BorderThickness = new Thickness(0) };
}
