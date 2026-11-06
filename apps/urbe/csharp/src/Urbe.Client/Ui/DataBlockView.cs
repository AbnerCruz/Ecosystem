using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Urbe.Core.Data;

namespace Urbe.Client.Ui;

/// <summary>
/// CST-207: a ```dados block in the note — the source's rows after the block's steps, read again from the vault each
/// time the note is shown (the note keeps the source and the steps, never a copy). Large results show their first
/// rows and say how many there are; a mistake in a step is shown in words in place of the table.
/// </summary>
public sealed class DataBlockView : Border
{
    /// <summary>Rows drawn at once; the rest are counted (a table of 200 000 rows would freeze a phone).</summary>
    public const int ShownRows = 300;

    private readonly StackPanel _panel = new() { Spacing = 6 };
    private readonly double _size;
    public DataResult? Result { get; private set; }
    public string? Error { get; private set; }
    /// <summary>The chart shown, when the block ends with a “gráfico …” step.</summary>
    public Image? Chart { get; private set; }
    public Task Reading { get; }

    public DataBlockView(string code, double size, Func<string, Task<string?>> readText)
    {
        _size = size;
        Child = _panel;
        Padding = new Thickness(12, 10);
        BorderThickness = new Thickness(1);
        BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        CornerRadius = new CornerRadius(10);
        _panel.Children.Add(Caption("Lendo os dados…"));
        Reading = LoadAsync(code, readText);
    }

    private async Task LoadAsync(string code, Func<string, Task<string?>> readText)
    {
        // the sources the block names are read first (async), then the steps run off the UI thread
        var texts = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in code.Replace("\r", "").Split('\n'))
        {
            var t = line.Trim();
            var colon = t.IndexOf(':');
            if (colon <= 0 || t[..colon].Trim().ToLowerInvariant() is not ("fonte" or "source" or "de" or "from")) continue;
            var path = t[(colon + 1)..].Trim().Trim('"');
            if (path.IndexOf('#') is > 0 and var hash) path = path[..hash].Trim();
            texts[path] = await readText(path);
            if (texts[path] is null && !Path.HasExtension(path)) texts[path + ".md"] = await readText(path + ".md");
        }
        try
        {
            Result = await Task.Run(() => DataBlock.Run(code, p => texts.GetValueOrDefault(p)));
        }
        catch (InvalidDataException ex) { Error = ex.Message; }
        await Dispatcher.UIThread.InvokeAsync(Show);
    }

    private void Show()
    {
        _panel.Children.Clear();
        if (Result is not { } r)
        {
            _panel.Children.Add(new TextBlock { Text = Error, Foreground = UrbeTheme.Brush(UrbeTheme.Danger), FontSize = _size * .85, TextWrapping = TextWrapping.Wrap });
            return;
        }
        var t = r.Table;
        if (r.Chart is not null)
        {
            // CST-208: the chart the steps describe, drawn at the screen's density
            var svg = r.ChartSvg()!;
            if (SvgRaster.ToPng(svg, 1280, 800) is { } png)
            {
                using var stream = new MemoryStream(png.Png);
                var image = new Image { Source = new Avalonia.Media.Imaging.Bitmap(stream), Stretch = Stretch.Uniform, MaxHeight = 420 };
                Avalonia.Automation.AutomationProperties.SetName(image, "Gráfico de " + string.Join(", ", r.Chart.Values) + " a partir de " + t.Source);
                _panel.Children.Add(image);
                Chart = image;
            }
            _panel.Children.Add(Caption(t.Rows.Count + " ponto(s) · fonte: " + t.Source));
            return;
        }
        var grid = new Grid();
        foreach (var _ in t.Columns) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (int c = 0; c < t.Columns.Count; c++) Add(grid, 0, c, t.Columns[c], header: true, right: t.Types[c] == DataType.Number);
        var shown = Math.Min(ShownRows, t.Rows.Count);
        for (int i = 0; i < shown; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (int c = 0; c < t.Columns.Count; c++) Add(grid, i + 1, c, t.Rows[i][c].Text, header: false, right: t.Types[c] == DataType.Number);
        }
        _panel.Children.Add(new ScrollViewer { Content = grid, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        var count = t.Rows.Count == shown ? t.Rows.Count + " linha(s)" : "mostrando " + shown + " de " + t.Rows.Count.ToString("N0", Urbe.Core.Data.DataTable.PtBr) + " linhas";
        _panel.Children.Add(Caption(count + " · fonte: " + t.Source));
        foreach (var note in r.Notes) _panel.Children.Add(Caption(note));
        Avalonia.Automation.AutomationProperties.SetName(this, "Tabela de dados de " + t.Source + ", " + count);
    }

    private void Add(Grid grid, int row, int col, string text, bool header, bool right)
    {
        var cell = new TextBlock
        {
            Text = text, FontSize = _size * .88, FontWeight = header ? FontWeight.SemiBold : FontWeight.Normal, MaxWidth = 280, TextWrapping = TextWrapping.Wrap,
            Foreground = UrbeTheme.Brush(header ? UrbeTheme.Text : UrbeTheme.Text2), TextAlignment = right ? TextAlignment.Right : TextAlignment.Left
        };
        var border = new Border { Child = cell, Padding = new Thickness(8, 4), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(0, 0, 0, header ? 1 : .5) };
        Grid.SetRow(border, row);
        Grid.SetColumn(border, col);
        grid.Children.Add(border);
    }

    private TextBlock Caption(string text) => new() { Text = text, FontSize = _size * .75, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextWrapping = TextWrapping.Wrap };
}
