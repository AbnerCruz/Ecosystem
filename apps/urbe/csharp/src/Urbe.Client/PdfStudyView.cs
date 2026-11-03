using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core.Assistant;

namespace Urbe.Client;

public sealed record ReaderQuote(string Path, int Page, string Text, string Sha256, StudyAnnotationKind Kind = StudyAnnotationKind.Citation, int? TextOffset = null, int? TextLength = null, Urbe.Core.ReaderRegion? Region = null);

/// <summary>Selectable PDF text and resumable search using the existing document extractor, offline on both heads.
/// This is explicitly a text-layer view, never a substitute for figures, layout or OCR.</summary>
public sealed class PdfStudyView : Grid, IDisposable
{
    private readonly byte[] _bytes;
    private readonly string _path;
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private readonly TextBox _text = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
        FontSize = 17, Margin = new Thickness(0, 8), Background = Brushes.Transparent };
    private readonly TextBox _page = new() { Width = 56, MinHeight = 48, Text = "1" };
    private readonly TextBox _query = new() { PlaceholderText = "Buscar neste PDF", MinWidth = 100, MinHeight = 48 };
    private readonly StackPanel _results = new() { Spacing = 4 };
    private readonly Button _moreText = new Ui.UrbeButton("Continuar texto desta página") { IsVisible = false };
    private readonly Button _moreSearch = new Ui.UrbeButton("Buscar mais") { IsVisible = false };
    private readonly Button _previous = new Ui.UrbeButton("Anterior") { Padding = new Thickness(10, 0) };
    private readonly Button _next = new Ui.UrbeButton("Próxima") { Padding = new Thickness(10, 0) };
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

    public PdfStudyView(string path, byte[] bytes)
    {
        _path = path;
        _bytes = bytes;
        RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto");
        var go = new Ui.UrbeButton("Ir") { Padding = new Thickness(10, 0) };
        go.Click += async (_, _) => { if (int.TryParse(_page.Text, out int page)) await GoToPageAsync(page); };
        _previous.Click += async (_, _) => await GoToPageAsync(CurrentPage - 1);
        _next.Click += async (_, _) => await GoToPageAsync(CurrentPage + 1);
        var nav = new WrapPanel { Children = { _previous, _page, go, _next } };
        Children.Add(nav);
        var find = new Ui.UrbeButton("Buscar");
        find.Click += async (_, _) => await SearchAsync(_query.Text ?? "");
        var searching = new DockPanel();
        DockPanel.SetDock(find, Dock.Right);
        searching.Children.Add(find);
        searching.Children.Add(_query);
        SetRow(searching, 1); Children.Add(searching);
        var results = new ScrollViewer { Content = _results, MaxHeight = 150 };
        SetRow(results, 2); Children.Add(results);
        SetRow(_text, 3); Children.Add(_text);
        var quote = new Ui.UrbeButton("Adicionar seleção à nota");
        void Emit(StudyAnnotationKind kind)
        {
            if (_shown is not { } source || kind != StudyAnnotationKind.Bookmark && string.IsNullOrWhiteSpace(_text.SelectedText))
            { _status.Text = "Selecione primeiro o trecho que deseja citar."; return; }
            QuoteRequested?.Invoke(new(_path, source.Page, kind == StudyAnnotationKind.Bookmark ? "Marcador da página " + source.Page : _text.SelectedText, source.Sha256, kind, kind == StudyAnnotationKind.Bookmark ? null : source.Start + Math.Min(_text.SelectionStart,_text.SelectionEnd), kind == StudyAnnotationKind.Bookmark ? null : _text.SelectedText.Length));
        }
        quote.Click += (_, _) => Emit(StudyAnnotationKind.Citation);
        var highlight = new Ui.UrbeButton("Destacar"); highlight.Click += (_, _) => Emit(StudyAnnotationKind.Highlight);
        var comment = new Ui.UrbeButton("Comentar"); comment.Click += (_, _) => Emit(StudyAnnotationKind.Comment);
        var bookmark = new Ui.UrbeButton("Marcar página"); bookmark.Click += (_, _) => Emit(StudyAnnotationKind.Bookmark);
        var ask = new Ui.UrbeButton("Perguntar à IA");
        ask.Click += (_, _) =>
        {
            if (_shown is { } source && !string.IsNullOrWhiteSpace(_text.SelectedText)) AskRequested?.Invoke(new(_path, source.Page, _text.SelectedText, source.Sha256));
            else _status.Text = "Selecione primeiro o trecho desejado.";
        };
        _moreText.Click += async (_, _) => { if (_shown is { More: true } source) await LoadPageAsync(source.Page, source.Next); };
        _moreSearch.Click += async (_, _) => await SearchAsync(_searchQuery, more: true);
        var footer = new StackPanel { Children = { _status, new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { _moreText, _moreSearch, quote, highlight, comment, bookmark, ask } } } } };
        SetRow(footer, 4); Children.Add(footer);
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
            _text.Text = result.Text;
            _previous.IsEnabled = CurrentPage > 1; _next.IsEnabled = CurrentPage < PageCount;
            _moreText.IsVisible = result.More;
            _status.Text = $"Página {CurrentPage} / {PageCount} · " + (result.NeedsOcr
                ? "Sem camada de texto. Use Reconhecer região (OCR) para ler a imagem da página."
                : "Texto extraído do PDF. Imagens, fórmulas e layout devem ser conferidos na página original.");
            PageChanged?.Invoke(CurrentPage);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { if (!_disposed && !token.IsCancellationRequested) _status.Text = "Não foi possível extrair esta página. O arquivo original foi preservado."; }
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
                var b = new Button { Content = new TextBlock { Text = $"p. {hit.Page} · {hit.Context}", TextWrapping = TextWrapping.Wrap },
                    HorizontalAlignment = HorizontalAlignment.Stretch };
                b.Click += async (_, _) => await GoToPageAsync(hit.Page);
                _results.Children.Add(b);
            }
            _moreSearch.IsVisible = result.Partial;
            _status.Text = $"Busca até a página {result.LastPage} / {result.Pages}. " +
                (result.Partial ? "Use Buscar mais para continuar." : "Busca concluída.") +
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
