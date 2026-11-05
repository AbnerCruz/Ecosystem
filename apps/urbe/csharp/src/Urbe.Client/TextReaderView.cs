using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// "Buscar no texto" for the plain text readers (CST-238): the query, "3 de 12", previous and next. Enter goes to the
/// next match, Shift+Enter to the previous one.
/// </summary>
public sealed class TextFindBar : DockPanel
{
    public TextBox Box { get; } = Ui.SoftField.Style(new TextBox { PlaceholderText = "Buscar no texto" });
    private readonly TextBlock _count = new() { VerticalAlignment = VerticalAlignment.Center, FontSize = 12, Margin = new Thickness(8, 0), MinWidth = 64 };
    public event Action<string>? QueryChanged;
    public event Action<int>? Step;

    public TextFindBar()
    {
        Margin = new Thickness(12, 0, 12, 8);
        _count.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        Box.MinHeight = 36;
        Box.FontSize = 14;
        var previous = new Ui.UrbeIconButton(UrbeTheme.Icons.ArrowUp, "Resultado anterior", 20);
        previous.Click += (_, _) => Step?.Invoke(-1);
        var next = new Ui.UrbeIconButton(UrbeTheme.Icons.ArrowDown, "Próximo resultado", 20);
        next.Click += (_, _) => Step?.Invoke(1);
        SetDock(next, Dock.Right);
        SetDock(previous, Dock.Right);
        SetDock(_count, Dock.Right);
        Children.Add(next);
        Children.Add(previous);
        Children.Add(_count);
        Children.Add(Box);
        Box.TextChanged += (_, _) => QueryChanged?.Invoke(Box.Text ?? "");
        Box.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            Step?.Invoke(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
            e.Handled = true;
        };
    }

    public string Count { get => _count.Text ?? ""; set => _count.Text = value; }
}

/// <summary>
/// The plain text reader (CST-238): TXT, LOG, RTF's text, formatted JSON, sources and listings. The whole text is
/// searched, but only a window of it is laid out: parts of <see cref="FilePreviewPanel.TextPart"/> ending at a line,
/// "Mostrar mais" and "Mostrar anterior" around them, and a match far away moves the window there, so a log of
/// hundreds of MB opens and searches without freezing the screen. The reader says how much of the file is shown.
/// </summary>
public sealed class TextReaderView : DockPanel
{
    private readonly string _text;
    private readonly StackPanel _parts = new();
    private readonly ScrollViewer _scroll;
    private readonly TextBlock _status = new() { FontSize = 12, Margin = new Thickness(12, 0, 12, 6), TextWrapping = TextWrapping.Wrap };
    private readonly Button _earlier = new() { Margin = new Thickness(12, 0, 12, 8), HorizontalAlignment = HorizontalAlignment.Left, Content = "Mostrar anterior" };
    private readonly Button _more = new() { Margin = new Thickness(12, 8), HorizontalAlignment = HorizontalAlignment.Left };
    private readonly List<(int Start, int End, SelectableTextBlock Block)> _shown = [];
    private Task<string>? _folded;
    private IReadOnlyList<int> _matches = [];
    private int _queryLength, _searchVersion;
    private SelectableTextBlock? _marked;

    public TextFindBar Find { get; } = new();
    public string Text => _text;
    /// <summary>The laid out window [ShownStart, ShownEnd) of the text.</summary>
    public int ShownStart => _shown.Count > 0 ? _shown[0].Start : 0;
    public int ShownEnd => _shown.Count > 0 ? _shown[^1].End : 0;
    public int MatchCount => _matches.Count;
    /// <summary>The match shown, from 0, or -1.</summary>
    public int CurrentMatch { get; private set; } = -1;
    public string StatusText => _status.Text ?? "";

    public TextReaderView(string text, IReadOnlyList<string>? notices = null)
    {
        _text = text;
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        _status.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        var top = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        foreach (var notice in notices ?? [])
            top.Children.Add(new TextBlock { Text = notice, FontSize = 12, Margin = new Thickness(12, 0, 12, 6), TextWrapping = TextWrapping.Wrap, Foreground = UrbeTheme.Brush(UrbeTheme.Warn) });
        top.Children.Add(Find);
        top.Children.Add(_status);
        SetDock(top, Dock.Top);
        Children.Add(top);
        _scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = new Border { Padding = new Thickness(0, 4, 0, 12), Child = _parts }
        };
        Children.Add(_scroll);
        _earlier.Click += (_, _) => Earlier();
        _more.Click += (_, _) => Later();
        Find.QueryChanged += query => _ = SearchAsync(query);
        Find.Step += step => GoToMatch(CurrentMatch + step);
        Later();
    }

    private TextReaderLook? _look;

    private SelectableTextBlock Part(string piece)
    {
        var block = new SelectableTextBlock
        {
            Text = piece, FontFamily = new FontFamily("Consolas, Menlo, monospace"), Padding = new Thickness(12, 0),
            SelectionBrush = UrbeTheme.Brush(UrbeTheme.Accent, .45)
        };
        Style(block);
        return block;
    }

    private void Style(SelectableTextBlock block)
    {
        block.FontSize = _look?.FontSize ?? 13;
        block.LineHeight = _look?.LinePixels ?? 19;
        block.Foreground = _look?.Colors?.Foreground ?? UrbeTheme.Brush(UrbeTheme.Text2);
    }

    /// <summary>CST-241 "Aa": the text readers' look (size, line spacing, theme), applied to every laid out part.</summary>
    public void ApplyLook(TextReaderLook look)
    {
        _look = look;
        Background = look.Colors?.Background ?? UrbeTheme.Brush(UrbeTheme.Bg);
        foreach (var (_, _, block) in _shown) Style(block);
    }

    /// <summary>The end of the part that starts at <paramref name="start"/>: a line end, never inside a surrogate pair.</summary>
    private int PartEnd(int start)
    {
        int end = Math.Min(_text.Length, start + FilePreviewPanel.TextPart);
        if (end < _text.Length && _text.LastIndexOf('\n', end - 1, end - start) is var nl and >= 0 && nl + 1 > start) end = nl + 1;
        if (end < _text.Length && char.IsHighSurrogate(_text[end - 1])) end++;
        return end;
    }

    /// <summary>"Mostrar mais": the next part after the window.</summary>
    public void Later()
    {
        int start = ShownEnd;
        if (_shown.Count > 0 && start >= _text.Length) return;
        int end = PartEnd(start);
        var block = Part(_text[start..end]);
        _shown.Add((start, end, block));
        _parts.Children.Remove(_more);
        _parts.Children.Add(block);
        Refresh();
    }

    /// <summary>"Mostrar anterior": the part before the window (after a jump to a far match).</summary>
    public void Earlier()
    {
        int end = ShownStart;
        if (end <= 0) return;
        int start = Math.Max(0, end - FilePreviewPanel.TextPart);
        if (start > 0 && _text.LastIndexOf('\n', start) is var nl and >= 0 && end - (nl + 1) <= FilePreviewPanel.TextPart * 2) start = nl + 1;
        if (start > 0 && char.IsLowSurrogate(_text[start])) start--;
        var block = Part(_text[start..end]);
        _shown.Insert(0, (start, end, block));
        _parts.Children.Insert(_parts.Children.Contains(_earlier) ? 1 : 0, block);
        Refresh();
    }

    /// <summary>The window starts again at the line of <paramref name="offset"/> (a match outside it).</summary>
    private void Reset(int offset)
    {
        _shown.Clear();
        _parts.Children.Clear();
        _marked = null;
        int start = offset == 0 ? 0 : _text.LastIndexOf('\n', offset - 1) + 1;
        if (offset - start > FilePreviewPanel.TextPart / 2) start = Math.Max(0, offset - 1024);
        if (start > 0 && char.IsLowSurrogate(_text[start])) start--;
        int end = PartEnd(start);
        var block = Part(_text[start..end]);
        _shown.Add((start, end, block));
        _parts.Children.Add(block);
        Refresh();
    }

    private void Refresh()
    {
        if (ShownStart > 0 && !_parts.Children.Contains(_earlier)) _parts.Children.Insert(0, _earlier);
        if (ShownStart == 0) _parts.Children.Remove(_earlier);
        int left = _text.Length - ShownEnd;
        if (left > 0)
        {
            _more.Content = "Mostrar mais (" + VaultFileKinds.FormatBytes(Encoding.UTF8.GetByteCount(_text.AsSpan(ShownEnd))) + " restantes)";
            if (!_parts.Children.Contains(_more)) _parts.Children.Add(_more);
        }
        else _parts.Children.Remove(_more);
        bool whole = ShownStart == 0 && ShownEnd >= _text.Length;
        _status.Text = whole ? "" : "Mostrando " + VaultFileKinds.FormatBytes(Encoding.UTF8.GetByteCount(_text.AsSpan(ShownStart, ShownEnd - ShownStart)))
            + " de " + VaultFileKinds.FormatBytes(Encoding.UTF8.GetByteCount(_text)) + "; a busca percorre o arquivo inteiro.";
        _status.IsVisible = !whole;
    }

    /// <summary>Finds every match of <paramref name="query"/> (without case or accents) and shows the first.</summary>
    public async Task SearchAsync(string query)
    {
        int version = ++_searchVersion;
        if (string.IsNullOrEmpty(query))
        {
            _matches = [];
            CurrentMatch = -1;
            Find.Count = "";
            Unmark();
            return;
        }
        _folded ??= Task.Run(() => TextSearch.Fold(_text));
        var folded = await _folded;
        if (version != _searchVersion) return;
        var matches = await Task.Run(() => TextSearch.FindAll(folded, query));
        if (version != _searchVersion) return;
        _matches = matches;
        _queryLength = query.Length;
        if (matches.Count == 0) { CurrentMatch = -1; Find.Count = "Nenhum"; Unmark(); return; }
        GoToMatch(0);
    }

    public void GoToMatch(int index)
    {
        if (_matches.Count == 0) return;
        index = (index % _matches.Count + _matches.Count) % _matches.Count;
        CurrentMatch = index;
        Find.Count = (index + 1) + " de " + _matches.Count + (_matches.Count >= TextSearch.MaxMatches ? "+" : "");
        int at = _matches[index];
        // a match just past the window is reached by laying out the next part; a far one moves the window
        if (at >= ShownEnd && at < ShownEnd + FilePreviewPanel.TextPart) Later();
        if (at < ShownStart || at >= ShownEnd) Reset(at);
        var (start, _, block) = _shown.First(p => at >= p.Start && at < p.End);
        Unmark();
        int local = at - start;
        block.SelectionStart = local;
        block.SelectionEnd = Math.Min(block.Text?.Length ?? 0, local + _queryLength);
        _marked = block;
        Dispatcher.UIThread.Post(() => Reveal(block, local), DispatcherPriority.Background);
    }

    /// <summary>The selected text of the match shown (tests).</summary>
    public string? MarkedText => _marked?.SelectedText;

    private void Unmark()
    {
        if (_marked is null) return;
        _marked.SelectionStart = _marked.SelectionEnd = 0;
        _marked = null;
    }

    private void Reveal(SelectableTextBlock block, int local)
    {
        if (!ReferenceEquals(block, _marked) || block.Parent is null) return;
        var rect = block.TextLayout.HitTestTextPosition(Math.Clamp(local, 0, block.Text?.Length ?? 0));
        if (block.TranslatePoint(rect.TopLeft, _parts) is not { } point) return;
        _scroll.Offset = new Vector(Math.Max(0, point.X - _scroll.Viewport.Width / 2), Math.Max(0, point.Y - _scroll.Viewport.Height / 3));
    }
}

/// <summary>
/// A CSV/TSV file as a table (CST-238): the first row is the header, the rows are virtualized (only the ones on the
/// screen exist as controls, parsed when shown), the header scrolls sideways with them, and the search finds rows.
/// </summary>
public sealed class CsvTableView : DockPanel
{
    private readonly CsvTable _table;
    private readonly string _text;
    private readonly double[] _widths;
    private readonly ListBox _rows;
    private Task<string>? _folded;
    private IReadOnlyList<int> _matchRows = [];
    private int _searchVersion;

    public TextFindBar Find { get; } = new();
    public int RowCount => Math.Max(0, _table.RowCount - 1);
    public int ColumnCount => _widths.Length;
    public IReadOnlyList<string> Header { get; }
    public int MatchCount => _matchRows.Count;
    public int CurrentMatch { get; private set; } = -1;
    /// <summary>The data row selected by the search, from 0, or -1.</summary>
    public int SelectedRow => _rows.SelectedIndex;
    public CsvTable Table => _table;

    public CsvTableView(string text, CsvTable table, IReadOnlyList<string>? notices = null)
    {
        _text = text;
        _table = table;
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        Header = table.RowCount > 0 ? table.Row(0) : [];
        // column widths from the first rows' longest cells, in the range a phone can still scroll through
        var sample = Enumerable.Range(0, Math.Min(table.RowCount, 200)).Select(table.Row).ToArray();
        int columns = sample.Length == 0 ? 0 : sample.Max(r => r.Length);
        _widths = Enumerable.Range(0, columns).Select(c => Math.Clamp(sample.Max(r => c < r.Length ? Math.Min(r[c].Length, 60) : 0) * 7.5 + 20, 56, 360)).ToArray();
        var top = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        foreach (var notice in notices ?? [])
            top.Children.Add(new TextBlock { Text = notice, FontSize = 12, Margin = new Thickness(12, 0, 12, 6), TextWrapping = TextWrapping.Wrap, Foreground = UrbeTheme.Brush(UrbeTheme.Warn) });
        top.Children.Add(Find);
        top.Children.Add(new TextBlock
        {
            Text = RowCount.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) + (RowCount == 1 ? " linha" : " linhas") + " × " + columns + (columns == 1 ? " coluna" : " colunas")
                + " · separador " + (table.Delimiter == '\t' ? "tabulação" : "“" + table.Delimiter + "”"),
            FontSize = 12, Margin = new Thickness(12, 0, 12, 6), Foreground = UrbeTheme.Brush(UrbeTheme.Text3)
        });
        SetDock(top, Dock.Top);
        Children.Add(top);
        var header = Row(Header, bold: true);
        _rows = new ListBox
        {
            ItemsSource = Enumerable.Range(1, RowCount).ToArray(),
            ItemTemplate = new FuncDataTemplate<int>((_, _) => new CsvRow(this), supportsRecycling: true),
            Background = Brushes.Transparent
        };
        var grid = new DockPanel { Width = _widths.Sum() + 24, HorizontalAlignment = HorizontalAlignment.Left };
        var headerBox = new Border { Child = header, BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(0, 0, 0, 1), Background = UrbeTheme.Brush(UrbeTheme.Surface) };
        SetDock(headerBox, Dock.Top);
        grid.Children.Add(headerBox);
        grid.Children.Add(_rows);
        Children.Add(new ScrollViewer
        {
            Content = grid,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        });
        Find.QueryChanged += query => _ = SearchAsync(query);
        Find.Step += step => GoToMatch(CurrentMatch + step);
    }

    private StackPanel Row(IReadOnlyList<string> cells, bool bold)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        Fill(row, cells, bold);
        return row;
    }

    internal void Fill(StackPanel row, IReadOnlyList<string> cells, bool bold)
    {
        row.Children.Clear();
        for (int c = 0; c < _widths.Length; c++)
            row.Children.Add(new TextBlock
            {
                Text = c < cells.Count ? cells[c].Replace('\n', ' ') : "", Width = _widths[c], Padding = new Thickness(6, 4),
                TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 13, FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal,
                Foreground = UrbeTheme.Brush(bold ? UrbeTheme.Text : UrbeTheme.Text2)
            });
    }

    /// <summary>One data row; recycled by the list, it parses its cells when its row changes.</summary>
    private sealed class CsvRow : StackPanel
    {
        private readonly CsvTableView _owner;
        public CsvRow(CsvTableView owner)
        {
            _owner = owner;
            Orientation = Orientation.Horizontal;
            DataContextChanged += (_, _) => { if (DataContext is int row && row < _owner._table.RowCount) _owner.Fill(this, _owner._table.Row(row), bold: false); };
        }
    }

    public async Task SearchAsync(string query)
    {
        int version = ++_searchVersion;
        if (string.IsNullOrEmpty(query)) { _matchRows = []; CurrentMatch = -1; Find.Count = ""; return; }
        _folded ??= Task.Run(() => TextSearch.Fold(_text));
        var folded = await _folded;
        if (version != _searchVersion) return;
        var rows = await Task.Run(() => TextSearch.FindAll(folded, query).Select(_table.RowAt).Where(r => r > 0).Distinct().ToArray());
        if (version != _searchVersion) return;
        _matchRows = rows;
        if (rows.Length == 0) { CurrentMatch = -1; Find.Count = "Nenhuma linha"; return; }
        GoToMatch(0);
    }

    public void GoToMatch(int index)
    {
        if (_matchRows.Count == 0) return;
        index = (index % _matchRows.Count + _matchRows.Count) % _matchRows.Count;
        CurrentMatch = index;
        Find.Count = (index + 1) + " de " + _matchRows.Count + (_matchRows.Count == 1 ? " linha" : " linhas");
        int item = _matchRows[index] - 1;
        _rows.SelectedIndex = item;
        _rows.ScrollIntoView(item);
    }
}
