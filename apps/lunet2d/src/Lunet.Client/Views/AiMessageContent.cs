using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Lunet.Ai;
using Lunet.Client.Ui;

namespace Lunet.Client.Views;

internal static class AiMessageContent
{
    public static StackPanel Render(string markdown, Action<string> copy)
    {
        var body = new StackPanel { Spacing = LunetTokens.Space.S };
        foreach (var block in ChatMarkdown.Parse(markdown))
        {
            body.Children.Add(block.Kind switch
            {
                ChatBlockKind.Code => Code(block, copy),
                ChatBlockKind.Table => Table(block.Rows!),
                ChatBlockKind.Rule => LunetUi.Divider(),
                ChatBlockKind.Quote => new Border
                {
                    BorderBrush = LunetTokens.Brush(LunetTokens.Color.Info),
                    BorderThickness = new Thickness(2, 0, 0, 0),
                    Padding = new Thickness(LunetTokens.Space.S, 0, 0, 0),
                    Child = Rich(block.Text),
                },
                ChatBlockKind.Bullet => ListItem("•", block.Text),
                ChatBlockKind.Numbered => ListItem($"{block.Level}.", block.Text),
                ChatBlockKind.Heading => Rich(block.Text, block.Level <= 2 ? LunetTokens.Type.TitleSmall : LunetTokens.Type.Callout, FontWeight.SemiBold),
                _ => Rich(block.Text),
            });
        }
        return body;
    }

    private static SelectableTextBlock Rich(string text, double size = LunetTokens.Type.BodySmall, FontWeight weight = FontWeight.Normal)
    {
        var result = new SelectableTextBlock
        {
            FontFamily = LunetTokens.UiFont,
            FontSize = size,
            FontWeight = weight,
            Foreground = LunetTokens.Brush(LunetTokens.Color.TextPrimary),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = size * 1.45,
        };
        foreach (var span in ChatMarkdown.Inline(text))
        {
            if (span.Style == ChatInline.Link && Uri.TryCreate(span.Target, UriKind.Absolute, out var uri)
                && uri.Scheme is "https" or "http")
            {
                var link = new HyperlinkButton
                {
                    Content = span.Text, NavigateUri = uri, Padding = new Thickness(0), MinHeight = 0,
                    Foreground = LunetTokens.Brush(LunetTokens.Color.Info), FontSize = size,
                };
                result.Inlines!.Add(new InlineUIContainer(link));
                continue;
            }

            var run = new Run(span.Text);
            switch (span.Style)
            {
                case ChatInline.Bold: run.FontWeight = FontWeight.SemiBold; break;
                case ChatInline.Italic: run.FontStyle = FontStyle.Italic; break;
                case ChatInline.Code:
                    run.FontFamily = LunetTokens.MonoFont;
                    run.Foreground = LunetTokens.Brush(LunetTokens.Color.Primary);
                    break;
            }
            result.Inlines!.Add(run);
        }
        return result;
    }

    private static Control ListItem(string marker, string text)
    {
        var label = LunetUi.Text(marker, LunetTokens.Type.BodySmall, LunetTokens.Color.TextSecondary);
        label.Margin = new Thickness(0, 2, LunetTokens.Space.Xs, 0);
        DockPanel.SetDock(label, Dock.Left);
        return new DockPanel { Children = { label, Rich(text) } };
    }

    private static Control Code(ChatBlock block, Action<string> copy)
    {
        var button = LunetUi.Button("Copiar", ButtonKind.Ghost, LunetIcons.Copy, () => copy(block.Text));
        button.Name = "AiCopyCode";
        Avalonia.Automation.AutomationProperties.SetName(button, "Copiar código");
        DockPanel.SetDock(button, Dock.Right);
        var language = LunetUi.Text(block.Language ?? "Código", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary, FontWeight.SemiBold);
        language.Margin = new Thickness(LunetTokens.Space.S, 0);
        language.VerticalAlignment = VerticalAlignment.Center;
        language.TextWrapping = TextWrapping.NoWrap;
        language.TextTrimming = TextTrimming.CharacterEllipsis;
        var header = new DockPanel
        {
            Background = LunetTokens.Brush(LunetTokens.Color.SurfaceRaised),
            Children = { button, language },
        };
        var code = new SelectableTextBlock
        {
            Text = block.Text, FontFamily = LunetTokens.MonoFont, FontSize = LunetTokens.Type.Label,
            Foreground = LunetTokens.Brush(LunetTokens.Color.Code), TextWrapping = TextWrapping.NoWrap,
            Margin = new Thickness(LunetTokens.Space.S),
        };
        return new Border
        {
            Background = LunetTokens.Brush(LunetTokens.Color.BackgroundSubtle),
            CornerRadius = new CornerRadius(LunetTokens.Radius.M),
            ClipToBounds = true,
            Child = new StackPanel { Children = { header, HorizontalScroll(code) } },
        };
    }

    private static Control Table(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var grid = new Grid();
        var columns = rows.Max(row => row.Count);
        for (var column = 0; column < columns; column++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (var row = 0; row < rows.Count; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var column = 0; column < rows[row].Count; column++)
            {
                var cell = new Border
                {
                    Background = LunetTokens.Brush(row == 0 ? LunetTokens.Color.SurfaceRaised : LunetTokens.Color.Surface),
                    BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding = new Thickness(LunetTokens.Space.S, LunetTokens.Space.Xs),
                    MinWidth = 76,
                    MaxWidth = 260,
                    Child = Rich(rows[row][column], LunetTokens.Type.Label,
                        row == 0 ? FontWeight.SemiBold : FontWeight.Normal),
                };
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, column);
                grid.Children.Add(cell);
            }
        }
        return new Border { CornerRadius = new CornerRadius(LunetTokens.Radius.M), ClipToBounds = true, Child = HorizontalScroll(grid) };
    }

    private static ScrollViewer HorizontalScroll(Control content) => new()
    {
        Content = content,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
    };
}
