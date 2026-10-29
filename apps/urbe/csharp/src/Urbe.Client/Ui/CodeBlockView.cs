using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Urbe.Client.Ui;

/// <summary>Any language, including plain text. Copy uses the current source, never rendered labels or fences.</summary>
/// <remarks>
/// A text of any length is shown whole: a long one (a tool's result, a big code block) is made of pieces of up to
/// <see cref="PieceLines"/> lines that are made as they come near the screen (<see cref="Deferred"/>), instead of pages
/// or one enormous text to shape at once. Copy still copies all of it; wrapping applies to every piece.
/// </remarks>
public sealed class CodeBlockView : Border
{
    public const int PieceChars = 3000, PieceLines = 80;
    private readonly string? _whole;
    private readonly List<SelectableTextBlock> _pieces = [];
    private readonly double _size;
    private TextWrapping _wrapping = TextWrapping.NoWrap;

    /// <summary>The text (the first piece of a long one).</summary>
    public SelectableTextBlock TextView { get; }
    public Button CopyButton { get; }
    public Button WrapButton { get; }
    public string SourceText => _whole ?? TextView.Text ?? "";
    /// <summary>The pieces of a long text (one for a short text); those far from the screen wait.</summary>
    public Panel? Pieces { get; }

    public CodeBlockView(string source, string language = "", double size = 13)
    {
        Padding = new Thickness(12, 4, 12, 12);
        CornerRadius = new CornerRadius(UrbeTokens.Radius.M);
        Background = UrbeTheme.Brush(UrbeTheme.Surface);
        _size = size;
        Control body;
        if (source.Length <= PieceChars) body = TextView = Piece(source);
        else
        {
            _whole = source;
            var pieces = new StackPanel();
            foreach (var piece in Split(source))
            {
                int lines = 1;
                foreach (var c in piece) if (c == '\n') lines++;
                pieces.Children.Add(new Deferred(() => Piece(piece), lines * size * 1.35));
            }
            Deferred.Watch(pieces);
            TextView = (SelectableTextBlock)((Deferred)pieces.Children[0]).View;
            Pieces = pieces;
            body = pieces;
        }
        _scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = body };
        CopyButton = ActionButton("Copiar");
        Avalonia.Automation.AutomationProperties.SetName(CopyButton, "Copiar bloco de código");
        CopyButton.Click += async (_, _) =>
        {
            CopyButton.Content = await TextActions.CopyAsync(this, SourceText) ? "Copiado" : "Falha ao copiar";
        };
        WrapButton = ActionButton("Quebrar linhas");
        WrapButton.Click += (_, _) =>
        {
            bool wrap = _wrapping == TextWrapping.NoWrap;
            _wrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
            foreach (var piece in _pieces) piece.TextWrapping = _wrapping;
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

    private SelectableTextBlock Piece(string text)
    {
        var view = new SelectableTextBlock { Text = text, FontFamily = UrbeTheme.MonoFont,
            FontSize = _size, Foreground = UrbeTheme.Brush(UrbeTheme.Text), TextWrapping = _wrapping };
        view.Classes.Add(MarkdownReader.TextClass);
        TextActions.Attach(view);
        _pieces.Add(view);
        return view;
    }

    /// <summary>
    /// Pieces of whole lines (up to <see cref="PieceLines"/> lines or about <see cref="PieceChars"/> characters); a single
    /// line longer than that (minified JSON, a data URL) goes on in the next piece.
    /// </summary>
    public static IEnumerable<string> Split(string text)
    {
        int start = 0;
        while (start < text.Length)
        {
            int end = start, lines = 0;
            while (end < text.Length && lines < PieceLines && end - start < PieceChars)
            {
                int nl = text.IndexOf('\n', end);
                int lineEnd = nl < 0 ? text.Length : nl + 1;
                if (lineEnd - start > PieceChars)
                {
                    // the line does not fit: the piece ends before it, or (a line alone) where the size ends
                    if (end == start)
                    {
                        end = Math.Min(text.Length, start + PieceChars);
                        if (end < text.Length && char.IsHighSurrogate(text[end - 1])) end--;   // never half a character
                    }
                    break;
                }
                end = lineEnd;
                lines++;
            }
            // a piece ends with its line break and the next one starts at its line; the last keeps a final break (an
            // empty last line)
            yield return end < text.Length && text[end - 1] == '\n' ? text[start..(end - 1)] : text[start..end];
            start = end;
        }
    }
    private static Button ActionButton(string name) => new() { Content = name, FontSize = 11,
        MinHeight = UrbeTokens.MinTouch, Padding = new Thickness(8, 4),
        Background = Brushes.Transparent, Foreground = UrbeTheme.Brush(UrbeTheme.Accent), BorderThickness = new Thickness(0) };
}
