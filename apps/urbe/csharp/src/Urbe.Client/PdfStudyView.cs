using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core.Assistant;
using Urbe.Client.Ui;

namespace Urbe.Client;

public sealed record ReaderQuote(string Path, int Page, string Text, string Sha256, StudyAnnotationKind Kind = StudyAnnotationKind.Citation, int? TextOffset = null, int? TextLength = null, Urbe.Core.ReaderRegion? Region = null);

/// <summary>Selectable PDF text and resumable search using the existing document extractor, offline on both heads.
/// This is explicitly a text-layer view, never a substitute for figures, layout or OCR.</summary>
public sealed class PdfStudyView : Grid, IDisposable
{
    private readonly byte[] _bytes;
    private readonly string _path;
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(4, 6, 4, 0) };
    private readonly TextBox _text = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
        FontSize = 17, LineHeight = 27, Padding = new Thickness(16, 8), Margin = new Thickness(0, 4, 0, 0), Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
    private readonly TextBox _page = new() { Width = 52, MinHeight = 40, Text = "1", HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly TextBlock _pages = new() { VerticalAlignment = VerticalAlignment.Center, FontSize = 13, Margin = new Thickness(6, 0, 4, 0) };
    private readonly TextBox _query = new() { PlaceholderText = "Buscar", MinHeight = 40, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly StackPanel _results = new() { Spacing = 0 };
    private readonly Button _moreText = new Ui.UrbeButton("Continuar texto desta página", ButtonKind.Ghost) { IsVisible = false };
    private readonly Button _moreSearch = new Ui.UrbeButton("Buscar mais", ButtonKind.Ghost) { IsVisible = false };
    private readonly Button _previous = new Ui.UrbeIconButton(UrbeTheme.Icons.Back, "Anterior");
    private readonly Button _next = new Ui.UrbeIconButton(UrbeTheme.Icons.Forward, "Próxima");
    private readonly Border _selectionBar = new()
    {
        IsVisible = false, CornerRadius = new CornerRadius(UrbeTokens.Radius.L), Padding = new Thickness(4, 0),
        Background = UrbeTheme.Brush(UrbeTheme.Surface2), BoxShadow = UrbeTokens.Elevation.Low, HorizontalAlignment = HorizontalAlignment.Center
    };
    private CancellationTokenSource _navigation = new(), _search = new();
    private bool _disposed;
    private DocumentPage? _shown;
    private DocumentSearch? _lastSearch;
    private string _searchQuery = "";

    public int CurrentPage { get; private set; } = 1;
    public int PageCount { get; private set; }
    public string ShownText => _text.Text ?? "";
    public event Action<ReaderQuote>? QuoteRequested;
    public event Action<ReaderQuote>? AskRequested;
    public event Action<int>? PageChanged;
    /// <summary>The page could not be read: the host offers another app.</summary>
    public event Action? Failed;

    public PdfStudyView(string path, byte[] bytes)
    {
        _path = path;
        _bytes = bytes;
        // search on top · the page's text · page ‹ 3 / 120 › at the bottom; the selection's actions only with a selection
        RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto");
        _status.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        _status.PropertyChanged += (_, e) => { if (e.Property == TextBlock.TextProperty) _status.IsVisible = !string.IsNullOrEmpty(_status.Text); };
        _pages.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        _text.Foreground = UrbeTheme.Brush(UrbeTheme.Text);
        _page.KeyDown += async (_, e) => { if (e.Key == Avalonia.Input.Key.Enter && int.TryParse(_page.Text, out int page)) { e.Handled = true; await GoToPageAsync(page); } };
        _page.LostFocus += async (_, _) => { if (int.TryParse(_page.Text, out int page) && page != CurrentPage) await GoToPageAsync(page); };
        _previous.Click += async (_, _) => await GoToPageAsync(CurrentPage - 1);
        _next.Click += async (_, _) => await GoToPageAsync(CurrentPage + 1);
        _query.KeyDown += async (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { e.Handled = true; await SearchAsync(_query.Text ?? ""); } };
        Avalonia.Automation.AutomationProperties.SetName(_page, "Página");
        Avalonia.Automation.AutomationProperties.SetName(_query, "Buscar neste PDF");
        _query.PlaceholderText = "Buscar no PDF";
        var top = new Border { Margin = new Thickness(12, 6, 12, 2), Child = Ui.SoftField.WithIcon(_query, UrbeTheme.Icons.SearchUi) };
        Ui.SoftField.Style(_page);
        _page.Padding = new Thickness(6, 4);
        _page.HorizontalContentAlignment = HorizontalAlignment.Center;
        Children.Add(top);
        var results = new ScrollViewer { Content = new StackPanel { Children = { _results, _moreSearch } }, MaxHeight = 168, Margin = new Thickness(8, 0) };
        SetRow(results, 1); Children.Add(results);
        SetRow(_text, 2); Children.Add(_text);
        var nav = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center, Children = { _previous, _page, _pages, _next } };
        SetRow(nav, 4); Children.Add(nav);
        void Emit(StudyAnnotationKind kind)
        {
            if (_shown is not { } source || kind != StudyAnnotationKind.Bookmark && string.IsNullOrWhiteSpace(_text.SelectedText))
            { _status.Text = "Selecione primeiro o trecho que deseja citar."; return; }
            QuoteRequested?.Invoke(new(_path, source.Page, kind == StudyAnnotationKind.Bookmark ? "Marcador da página " + source.Page : _text.SelectedText, source.Sha256, kind, kind == StudyAnnotationKind.Bookmark ? null : source.Start + Math.Min(_text.SelectionStart,_text.SelectionEnd), kind == StudyAnnotationKind.Bookmark ? null : _text.SelectedText.Length));
        }
        // the selection's actions: small labelled chips in one scrolling row (the label says what each does)
        Button Chip(string text, string icon, Action run)
        {
            var chip = new Ui.UrbeButton(text, ButtonKind.Ghost, icon) { MinHeight = 40, Padding = new Thickness(10, 0) };
            chip.Click += (_, _) => run();
            return chip;
        }
        var quote = Chip("Adicionar seleção à nota", UrbeTheme.Icons.Notes, () => Emit(StudyAnnotationKind.Citation));
        var highlight = Chip("Destacar", UrbeTheme.Icons.Pencil, () => Emit(StudyAnnotationKind.Highlight));
        var comment = Chip("Comentar", UrbeTheme.Icons.Edit, () => Emit(StudyAnnotationKind.Comment));
        var bookmark = new Ui.UrbeIconButton(UrbeTheme.Icons.Pin, "Marcar página", 20);
        bookmark.Click += (_, _) => Emit(StudyAnnotationKind.Bookmark);
        var ask = Chip("Perguntar à IA", UrbeTheme.Icons.Assistant, () =>
        {
            if (_shown is { } source && !string.IsNullOrWhiteSpace(_text.SelectedText)) AskRequested?.Invoke(new(_path, source.Page, _text.SelectedText, source.Sha256));
            else _status.Text = "Selecione primeiro o trecho desejado.";
        });
        _moreText.Click += async (_, _) => { if (_shown is { More: true } source) await LoadPageAsync(source.Page, source.Next); };
        _moreSearch.Click += async (_, _) => await SearchAsync(_searchQuery, more: true);
        _selectionBar.Child = new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { quote, highlight, comment, ask } } };
        _text.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.SelectionStartProperty || e.Property == TextBox.SelectionEndProperty)
                _selectionBar.IsVisible = _text.SelectionStart != _text.SelectionEnd;
        };
        // "Marcar página" needs no selection: it lives with the page navigation
        nav.Children.Add(bookmark);
        var footer = new StackPanel { Margin = new Thickness(8, 0), Children = { _moreText, _selectionBar, _status } };
        SetRow(footer, 3); Children.Add(footer);
        DetachedFromVisualTree += (_, _) => Dispose();
    }

    public Task GoToPageAsync(int page) => LoadPageAsync(PageCount > 0 ? Math.Clamp(page, 1, PageCount) : Math.Max(1, page), 0);

    public async Task GoToTextAsync(int page,int offset,int length)
    { await LoadPageAsync(page,offset); _text.SelectionStart=0;_text.SelectionEnd=Math.Min(length,_text.Text?.Length??0); }
    private async Task LoadPageAsync(int page, int offset)
    {
        if (_disposed) return;
        _navigation.Cancel(); _navigation.Dispose(); _navigation = new();
        var token = _navigation.Token;
        _shown = null; _text.Text = ""; _moreText.IsVisible = false;
        _status.Text = "Lendo camada de texto…";
        try
        {
            var result = await Task.Run(() => AssistantDocumentTools.Page(_bytes, page, offset, 16000), token);
            if (_disposed || token.IsCancellationRequested) return;
            _shown = result;
            CurrentPage = result.Page; PageCount = result.Pages;
            _page.Text = CurrentPage.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _pages.Text = "/ " + PageCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _text.Text = result.Text;
            _previous.IsEnabled = CurrentPage > 1; _next.IsEnabled = CurrentPage < PageCount;
            _moreText.IsVisible = result.More;
            _status.Text = result.NeedsOcr
                ? "Esta página não tem texto, só imagem: use Reconhecer região (OCR) na barra acima."
                : "";
            PageChanged?.Invoke(CurrentPage);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (_disposed || token.IsCancellationRequested) return;
            _status.Text = "Não foi possível extrair esta página. O arquivo original foi preservado.";
            Failed?.Invoke();
        }
    }

    public async Task SearchAsync(string query, bool more = false)
    {
        if (_disposed || string.IsNullOrWhiteSpace(query)) return;
        _search.Cancel(); _search.Dispose(); _search = new();
        var token = _search.Token;
        var prior = more && query == _searchQuery ? _lastSearch : null;
        if (prior is null) _results.Children.Clear();
        _searchQuery = query; _moreSearch.IsVisible = false;
        _status.Text = "Pesquisando a camada de texto…";
        try
        {
            var result = await Task.Run(() => AssistantDocumentTools.Search(_bytes, query,
                start: prior?.NextPage ?? 1, startChar: prior?.NextChar ?? 0, cancellationToken: token), token);
            if (_disposed || token.IsCancellationRequested) return;
            _lastSearch = result;
            foreach (var hit in result.Matches)
            {
                var b = new Button
                {
                    Content = new TextBlock
                    {
                        Inlines = new Avalonia.Controls.Documents.InlineCollection { new Avalonia.Controls.Documents.Run("p. " + hit.Page + "  ") { FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Accent) }, new Avalonia.Controls.Documents.Run(hit.Context) },
                        TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text2)
                    },
                    HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
                    Background = Brushes.Transparent, Padding = new Thickness(8, 8), CornerRadius = new CornerRadius(UrbeTokens.Radius.S)
                };
                b.Click += async (_, _) => await GoToPageAsync(hit.Page);
                _results.Children.Add(b);
            }
            _moreSearch.IsVisible = result.Partial;
            _status.Text = (result.Matches.Count == 0 && prior is null ? "Nada encontrado" : result.Matches.Count + " resultado(s)") +
                (result.Partial ? $" até a página {result.LastPage} de {result.Pages}." : ".") +
                (result.OcrPages > 0 ? " Páginas sem texto não foram pesquisadas por OCR." : "");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { if (!_disposed && !token.IsCancellationRequested) _status.Text = "Não foi possível pesquisar este PDF."; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _navigation.Cancel(); _navigation.Dispose(); _search.Cancel(); _search.Dispose();
    }
}
