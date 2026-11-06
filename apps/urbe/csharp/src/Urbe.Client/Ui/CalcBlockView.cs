using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core.Calculus;

namespace Urbe.Client.Ui;

/// <summary>
/// CST-206: a ```calculo block in the note — each line beside its result, computed as the note is read (the results
/// are never written into the note). A line that could not be computed says why, in its own place. A tap goes on to
/// the block, which opens its lines for editing like any other block.
/// </summary>
public sealed class CalcBlockView : Border
{
    public IReadOnlyList<CalcLine> Lines { get; }

    public CalcBlockView(string code, double size)
    {
        Lines = Calculator.Run(code);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,2*"), RowSpacing = 6 };
        int row = 0;
        foreach (var line in Lines)
        {
            if (line.Input.Trim().Length == 0) continue;
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            // a definition shows its value (preco = 12,5 → 12,5); a function definition needs nothing beside it
            var shown = line.Ok && line.Definition && line.Result is { } def ? (def.Contains('(') && def.IndexOf('(') < def.IndexOf('=') ? null : def[(def.IndexOf(" = ", StringComparison.Ordinal) + 3)..]) : line.Ok ? line.Result : line.Error;
            var comment = shown is null;
            var input = new TextBlock
            {
                Text = line.Input.Trim(), FontFamily = UrbeTheme.MonoFont, FontSize = size * .88, TextWrapping = TextWrapping.Wrap,
                Foreground = UrbeTheme.Brush(comment ? UrbeTheme.Text3 : UrbeTheme.Text2), VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(input, row);
            grid.Children.Add(input);
            if (!comment)
            {
                var result = new TextBlock
                {
                    Text = shown, FontSize = line.Ok ? size : size * .8, TextWrapping = line.Ok ? TextWrapping.WrapWithOverflow : TextWrapping.Wrap,
                    FontWeight = line.Ok && !line.Definition ? FontWeight.SemiBold : FontWeight.Normal, TextAlignment = TextAlignment.Right,
                    Foreground = UrbeTheme.Brush(!line.Ok ? UrbeTheme.Danger : line.Definition ? UrbeTheme.Text2 : UrbeTheme.Text), Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center
                };
                Avalonia.Automation.AutomationProperties.SetName(result, line.Input.Trim() + (line.Ok ? " resulta " : ": erro, ") + result.Text);
                Grid.SetRow(result, row);
                Grid.SetColumn(result, 1);
                grid.Children.Add(result);
            }
            row++;
        }
        Child = grid;
        Padding = new Thickness(12, 10);
        BorderThickness = new Thickness(1);
        BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        CornerRadius = new CornerRadius(10);
        Avalonia.Automation.AutomationProperties.SetName(this, "Cálculo com " + row + " linha(s)");
    }
}
