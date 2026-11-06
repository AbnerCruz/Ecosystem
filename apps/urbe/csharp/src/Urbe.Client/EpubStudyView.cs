using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client;

public sealed record BookReaderQuote(string Path, string Unit, int Index, string Text, string Sha256, int? TextOffset = null, int? TextLength = null, StudyAnnotationKind Kind = StudyAnnotationKind.Citation);

/// <summary>One spine item at a time, with independent typography, native HTML, text selection, search and note citations.</summary>
public sealed class EpubStudyView : Grid, IDisposable
{
    private readonly IReaderPublication _book;
    private readonly string _path;
    private readonly object _gate = new();
    private readonly ComboBox _chapters = new() { HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 40, VerticalAlignment = VerticalAlignment.Center };
    private readonly ContentControl _body = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly TextBox _text = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontSize = 18 };
    private readonly TextBox _query = new() { PlaceholderText = "Buscar no livro", MinHeight = 40, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly TextBlock _status = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 4, 4, 0) };
    private readonly ListBox _results = new() { IsVisible = false, MaxHeight = 160 };
    private readonly ProgressBar _progress = new() { Minimum = 0, Height = 4 };
    private readonly Ui.UrbeIconButton _previous = new(UrbeTheme.Icons.Back, "Capítulo anterior"), _next = new(UrbeTheme.Icons.Forward, "Próximo capítulo");
    private readonly Ui.UrbeButton _moreSearch = new("Buscar mais", Ui.ButtonKind.Ghost);
    private bool _updating, _textMode;
    private volatile bool _closed;
    private int _version;
    private ReaderChapterContent? _chapter;
    private IReaderTextSelection? _selection;
    private string _searchQuery = "";
    private int _searchPart, _searchOffset;
    public int CurrentChapter { get; private set; }
    /// <summary>The spine items (chapters, sections, slides): what reading progress is counted in.</summary>
    public int PartCount => _book.Chapters.Count;
    private readonly Avalonia.Threading.DispatcherTimer _positionTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private bool _capturing;
    public int CurrentOffset { get; private set; }
    public double CurrentScroll { get; private set; }
    private int _highlightLength;
    public string ThemePreference { get; private set; }="system";
    public double MarginPreference { get; private set; }=5;
    public double LineHeightPreference { get; private set; } = 1.6;
    public double FontSizePreference { get; private set; } = 18;
    public event Action<BookReaderQuote>? QuoteRequested;
    public event Action<BookReaderQuote>? AskRequested;
    public event Action<int>? PositionChanged;
    public event Action<string>? ExternalLinkRequested;
    /// <summary>CST-242: a text recognised in a picture of the book, to cite (by its chapter and region).</summary>
    public event Action<ImageStudyQuote>? ImageQuoteRequested;
    /// <summary>CST-242: a region of a picture of the book, to ask the AI about.</summary>
    public event Action<StudyImageQuestion>? VisualQuestionRequested;
    /// <summary>The pictures of the chapter shown now (read by tests).</summary>
    public IReadOnlyList<ReaderImage> ChapterPictures { get; private set; } = [];
    public OcrStudyView? FigureView { get; private set; }
    public string ShownText => _chapter?.Text ?? "";

    public EpubStudyView(string path, IReaderPublication book, bool compact = false)
    {
        _path = path; _book = book;
        RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto");
        _chapters.ItemsSource = book.Chapters.Select(c => c.Title).ToArray();
        _chapters.SelectionChanged += async (_, _) => { if (!_updating && _chapters.SelectedIndex >= 0) await GoToChapterAsync(_chapters.SelectedIndex); };
        _previous.Click += async (_, _) => await GoToChapterAsync(CurrentChapter - 1);
        _next.Click += async (_, _) => await GoToChapterAsync(CurrentChapter + 1);
        // one row: ‹ chapter › · Aa (type, spacing, theme, margins, text view) · search
        var type = new Ui.UrbeIconButton(UrbeTheme.Icons.TextLayer, "Aparência da leitura", 20);
        type.Click += (_, _) =>
        {
            var items = new List<MenuItem>();
            void Item(string header, Func<Task> run) { var i = new MenuItem { Header = header }; i.Click += async (_, _) => await run(); items.Add(i); }
            Item("Letra maior", () => SetTypographyAsync(FontSizePreference + 2));
            Item("Letra menor", () => SetTypographyAsync(FontSizePreference - 2));
            Item("Entrelinha: " + (LineHeightPreference >= 2 ? "1,2" : (LineHeightPreference + .2).ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))),
                async () => { await CaptureLocationAsync(); LineHeightPreference = LineHeightPreference >= 2 ? 1.2 : LineHeightPreference + .2; await ShowChapterAsync(); });
            Item("Tema: " + (ThemePreference switch { "system" => "claro", "light" => "sépia", "sepia" => "escuro", _ => "do sistema" }),
                async () => { await CaptureLocationAsync(); ThemePreference = ThemePreference switch { "system" => "light", "light" => "sepia", "sepia" => "dark", _ => "system" }; await ShowChapterAsync(); });
            Item(MarginPreference >= 10 ? "Sem margens" : "Margens maiores",
                async () => { await CaptureLocationAsync(); MarginPreference = MarginPreference >= 10 ? 0 : MarginPreference + 2.5; await ShowChapterAsync(); });
            Item(_textMode ? "Ver com a formatação do livro" : "Ver só o texto", async () => { _textMode = !_textMode; await ShowChapterAsync(); });
            new ContextMenu { ItemsSource = items }.Open(type);
        };
        var find = new Ui.UrbeIconButton(UrbeTheme.Icons.SearchUi, "Buscar", 20);
        var search = new DockPanel { IsVisible = false, Margin = new Thickness(0, 4, 0, 0) };
        find.Click += async (_, _) =>
        {
            if (!search.IsVisible) { search.IsVisible = true; _query.Focus(); return; }
            await SearchAsync(_query.Text ?? "");
        };
        _query.KeyDown += async (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { e.Handled = true; await SearchAsync(_query.Text ?? ""); } };
        var nav = new DockPanel { LastChildFill = true };
        // CST-242: the chapter's figures (diagrams, equations as images, scans) opened to read, recognise and ask about
        var figures = new Ui.UrbeIconButton(UrbeTheme.Icons.Image, "Imagens do capítulo", 20);
        figures.Click += async (_, _) => await ShowFiguresMenuAsync(figures);
        var buttonScroll = new StackPanel { Orientation = Orientation.Horizontal, Children = { type, figures, find } };
        DockPanel.SetDock(_previous, Dock.Left);
        DockPanel.SetDock(buttonScroll, Dock.Right);
        DockPanel.SetDock(_next, Dock.Right);
        nav.Children.Add(_previous);
        nav.Children.Add(buttonScroll);
        nav.Children.Add(_next);
        nav.Children.Add(_chapters);
        Children.Add(nav);
        search.Children.Add(Ui.SoftField.WithIcon(_query, UrbeTheme.Icons.SearchUi));
        SetRow(search, 1); Children.Add(search);
        _results.SelectionChanged += async (_, _) => { if (_results.SelectedItem is SearchHit hit) await GoToChapterAsync(hit.Part, hit.Offset, hit.Length); };
        SetRow(_results, 2); Children.Add(_results);
        SetRow(_body, 3); Children.Add(_body);
        Button Chip(string text, string icon, Func<Task> run)
        {
            var chip = new Ui.UrbeButton(text, Ui.ButtonKind.Ghost, icon) { MinHeight = 40, Padding = new Thickness(10, 0) };
            chip.Click += async (_, _) => await run();
            return chip;
        }
        var quote = Chip("Adicionar seleção à nota", UrbeTheme.Icons.Notes, () => WithSelectionAsync(false));
        var ask = Chip("Perguntar à IA", UrbeTheme.Icons.Assistant, () => WithSelectionAsync(true));
        var highlight = Chip("Destacar", UrbeTheme.Icons.Pencil, () => WithSelectionAsync(false, StudyAnnotationKind.Highlight));
        var comment = Chip("Comentar", UrbeTheme.Icons.Edit, () => WithSelectionAsync(false, StudyAnnotationKind.Comment));
        var bookmark = new Ui.UrbeButton("Marcar posição", Ui.ButtonKind.Ghost, UrbeTheme.Icons.Pin) { MinHeight = 40, Padding = new Thickness(10, 0) };
        bookmark.Click += async (_, _) =>
        {
            await CaptureLocationAsync();
            QuoteRequested?.Invoke(new(_path, _book.Unit, CurrentChapter + 1, _book.Chapters[CurrentChapter].Title, _book.Sha256, CurrentOffset, 0, StudyAnnotationKind.Bookmark));
        };
        _moreSearch.IsVisible = false; _moreSearch.Click += async (_, _) => await SearchAsync(_searchQuery, true);
        _status.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        var footer = new StackPanel { Children = { _progress, new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { quote, highlight, comment, bookmark, ask, _moreSearch } } }, _status } };
        SetRow(footer, 4); Children.Add(footer);
        _progress.Maximum = book.Chapters.Count;
        if (compact) { buttonScroll.IsVisible = search.IsVisible = footer.IsVisible = false; }
        _body.SizeChanged += (_, e) => { if (_body.Content is Control viewer) viewer.Height = Math.Max(1, e.NewSize.Height); };
        _positionTimer.Tick += async (_, _) => await CaptureLocationAsync();
        Loaded += (_, _) => _positionTimer.Start();
        DetachedFromVisualTree += (_, _) => Dispose();
    }

    public async Task GoToChapterAsync(int index, int offset = 0, int length = 0, double scroll = 0)
    {
        if (_closed) return;
        CurrentOffset = Math.Max(0, offset); _highlightLength = Math.Max(0, length); CurrentScroll = Math.Clamp(scroll, 0, 1);
        CurrentChapter = Math.Clamp(index, 0, _book.Chapters.Count - 1);
        _updating = true; _chapters.SelectedIndex = CurrentChapter; _updating = false;
        _previous.IsEnabled = CurrentChapter > 0; _next.IsEnabled = CurrentChapter < _book.Chapters.Count - 1;
        _progress.Value = CurrentChapter + 1;
        await ShowChapterAsync();
        if (!_closed) PositionChanged?.Invoke(CurrentChapter);
    }

    public async Task SetTypographyAsync(double size)
    { await CaptureLocationAsync(); FontSizePreference = Math.Clamp(size, 10, 48); await ShowChapterAsync(); }

    /// <summary>The pictures of the current chapter; read off the UI thread.</summary>
    public async Task<IReadOnlyList<ReaderImage>> LoadChapterPicturesAsync()
    {
        int chapter = CurrentChapter;
        var pictures = await Task.Run(() => { lock (_gate) return _closed ? [] : _book.ChapterImages(chapter); });
        if (chapter == CurrentChapter) ChapterPictures = pictures;
        return pictures;
    }

    private async Task ShowFiguresMenuAsync(Control anchor)
    {
        var pictures = await LoadChapterPicturesAsync();
        if (pictures.Count == 0) { _status.Text = "Este capítulo não tem imagens que possam ser examinadas (desenhos SVG não entram: o reconhecimento precisa de pixels)."; return; }
        var items = pictures.Select(picture =>
        {
            var item = new MenuItem { Header = "Imagem " + picture.Number + (picture.Description.Length > 0 ? " — " + (picture.Description.Length > 60 ? picture.Description[..59] + "…" : picture.Description) : " — sem descrição no livro") };
            item.Click += (_, _) => OpenFigure(picture);
            return item;
        }).ToList();
        new ContextMenu { ItemsSource = items }.Open(anchor);
    }

    /// <summary>A picture of the chapter in the region/recognition view, cited by its chapter; ‹ returns to the text.</summary>
    public void OpenFigure(ReaderImage picture)
    {
        OcrStudyView view;
        try
        {
            view = new OcrStudyView(_path, null, _book.Sha256, picture.Bytes)
            {
                Part = new PartSourceReference(_book.Unit, CurrentChapter + 1, _book.Sha256),
                ShowsBack = true
            };
        }
        // a picture whose bytes look right but do not decode (a damaged file inside the book) is said, never a crash
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        { _status.Text = "A imagem " + picture.Number + " não pôde ser aberta: o arquivo dela no livro está danificado ou num formato que o aparelho não decodifica."; return; }
        view.QuoteRequested += quote => ImageQuoteRequested?.Invoke(quote);
        view.VisualQuestionRequested += question => VisualQuestionRequested?.Invoke(question with { Path = _path });
        view.CloseRequested += async () => { FigureView = null; view.Dispose(); await ShowChapterAsync(); };
        FigureView?.Dispose();
        FigureView = view;
        _body.Content = view;
        _status.Text = "Imagem " + picture.Number + " do " + _book.PartLabel.ToLowerInvariant() + " " + (CurrentChapter + 1) +
            (picture.Description.Length > 0 ? ": " + picture.Description : "") + ". Selecione uma região para reconhecer o texto ou perguntar à IA.";
    }

    private async Task ShowChapterAsync()
    {
        int version = ++_version, chapter = CurrentChapter;
        if (_closed) return;
        _status.Text = "Lendo capítulo…";
        try
        {
            var loaded = await Task.Run(() => { lock (_gate) return _closed || version != _version ? null : _book.ReadChapter(chapter, FontSizePreference, LineHeightPreference); });
            if (_closed || version != _version || loaded is null) return;
            _chapter = loaded;
            _text.Text = loaded.Text; _text.FontSize = FontSizePreference;
            if (!_textMode)
            {
                bool presentation = _book is OfficeReadingDocument { IsPowerPoint: true };
                if (presentation && _body.Content is IReaderSlideRenderer slideRenderer)
                { await slideRenderer.ShowReaderPartAsync(chapter); }
                else if (_body.Content is IHtmlReloadable reuse)
                { reuse.Reload(loaded.Html); }
                else
                {
                    string html = presentation ? await Task.Run(() => PresentationReaderHtml.Build(((OfficeReadingDocument)_book).SourceBytes, chapter)) : loaded.Html;
                    if (_closed || version != _version) return;
                    var viewer = await ContentViewers.CreateAsync(new ContentRequest(null, _path, _book.Title, VaultFileKinds.Html, Encoding.UTF8.GetBytes(html))
                    { ReaderNavigate = FollowLink, ReaderEngine = presentation ? ReaderContentEngine.Presentation : ReaderContentEngine.Document });
                    if (_closed || version != _version) { (viewer as IDisposable)?.Dispose(); return; }
                    _selection = viewer as IReaderTextSelection;
                    _body.Content = viewer ?? _text;
                }
            }
            else { _selection = null; if (_body.Content is NativeControlHost native) native.IsVisible = false; _body.Content = _text; }
            if (_body.Content is IReaderTextLocation location) { await location.ApplyReaderAppearanceAsync(ThemePreference,MarginPreference,!loaded.FixedLayout);await location.RestoreTextPositionAsync(CurrentOffset, _highlightLength, CurrentScroll); }
            else if (_body.Content == _text && _highlightLength > 0) { _text.SelectionStart = Math.Min(CurrentOffset, _text.Text?.Length ?? 0); _text.SelectionEnd = (int)Math.Min((long)CurrentOffset + _highlightLength, _text.Text?.Length ?? 0); }
            _status.Text = $"{_book.PartLabel} {chapter + 1} / {_book.Chapters.Count}" + (loaded.FixedLayout ? " · layout fixo; tipografia original preservada" : " · texto refluível") + (_book.FidelityWarning is { } warning ? " · " + warning : "");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { if (!_closed && version == _version) _status.Text = "Não foi possível ler este capítulo: " + ex.Message; }
    }

    private void FollowLink(string target)
    {
        if (ReaderUris.Web(target) is not { } uri) return;
        if (uri.Host == "urbe.reader.invalid" && int.TryParse(uri.AbsolutePath.Split('/').LastOrDefault(), out int part)) _ = FollowChapterAsync(part,uri.Fragment.TrimStart('#'));
        else ExternalLinkRequested?.Invoke(uri.AbsoluteUri);
    }
    private async Task FollowChapterAsync(int part,string anchor)
    {
        await GoToChapterAsync(part);
        if (!_closed && anchor.Length>0 && _body.Content is IReaderTextLocation locator)await locator.GoToReaderAnchorAsync(Uri.UnescapeDataString(anchor));
    }
    private async Task WithSelectionAsync(bool ask, StudyAnnotationKind kind = StudyAnnotationKind.Citation)
    {
        int version = _version, chapter = CurrentChapter;
        try
        {
            string selection = _body.Content == _text ? _text.SelectedText : _selection is null ? "" : await _selection.SelectedTextAsync();
            if (_closed || version != _version) return;
            if (string.IsNullOrWhiteSpace(selection)) { _status.Text = "Selecione o trecho desejado no capítulo."; return; }
            ReaderTextPosition? position = _body.Content == _text
                ? new ReaderTextPosition(Math.Min(_text.SelectionStart, _text.SelectionEnd), Math.Abs(_text.SelectionEnd - _text.SelectionStart), selection, CurrentScroll)
                : _body.Content is IReaderTextLocation locator ? await locator.ReadTextPositionAsync(true) : null;
            if (_closed || version != _version) return;
            var quote = new BookReaderQuote(_path, _book.Unit, chapter + 1, selection, _book.Sha256, position?.Offset, position?.Length, kind);
            if (ask) AskRequested?.Invoke(quote); else QuoteRequested?.Invoke(quote);
        }
        catch (Exception ex) when (ex is TimeoutException or InvalidOperationException or ObjectDisposedException) { _status.Text = "A seleção não está disponível agora."; }
    }
    public async Task CaptureLocationAsync()
    {
        if (_closed || _capturing || _body.Content is not IReaderTextLocation locator) return;
        _capturing = true;
        try
        {
            var current = _body.Content;
            var position = await locator.ReadTextPositionAsync(false);
            if (!_closed && ReferenceEquals(current, _body.Content) && position is not null)
            { CurrentOffset = position.Offset; CurrentScroll = position.Scroll; PositionChanged?.Invoke(CurrentChapter); }
        }
        catch (Exception ex) when (ex is TimeoutException or InvalidOperationException or ObjectDisposedException) { }
        finally { _capturing = false; }
    }
    public void RestoreAppearance(string theme,double margin) {ThemePreference=theme;MarginPreference=Math.Clamp(margin,0,25);}
    public void RestoreLineHeight(double height) => LineHeightPreference = Math.Clamp(height, 1, 3);
    private sealed record SearchHit(int Part, int Offset, int Length, string Context)
    { public override string ToString() => $"Parte {Part + 1} · {Context}"; }
    public async Task SearchAsync(string query, bool more = false)
    {
        if (_closed || string.IsNullOrWhiteSpace(query)) return;
        if (!more || query != _searchQuery) { _searchPart = _searchOffset = 0; _results.ItemsSource = null; }
        _searchQuery = query;
        var hits = more && _results.ItemsSource is IEnumerable<SearchHit> previous ? previous.ToList() : [];
        int part = _searchPart, offset = _searchOffset;
        var batch = await Task.Run(() =>
        {
            var found = new List<SearchHit>();
            while (!_closed && part < _book.Chapters.Count && found.Count < 64)
            {
                string text;
                lock (_gate) { if (_closed) break; text = _book.ReadText(part); }
                while (offset < text.Length && found.Count < 64)
                {
                    int at = text.IndexOf(query, offset, StringComparison.OrdinalIgnoreCase);
                    if (at < 0) { offset = text.Length; break; }
                    found.Add(new(part, at, query.Length, text[Math.Max(0, at - 40)..Math.Min(text.Length, at + query.Length + 60)]));
                    offset = at + query.Length;
                }
                if (offset >= text.Length) { part++; offset = 0; }
            }
            return found;
        });
        if (_closed || query != _searchQuery) return;
        _searchPart = part; _searchOffset = offset; hits.AddRange(batch);
        _results.ItemsSource = hits; _results.IsVisible = hits.Count > 0; _moreSearch.IsVisible = part < _book.Chapters.Count;
        _status.Text = hits.Count + " resultados" + (part < _book.Chapters.Count ? " · Buscar mais continua a pesquisa" : " · pesquisa concluída");
    }
    public void Dispose()
    {
        if (_closed) return;
        _closed = true; _version++; _positionTimer.Stop();
        if (_body.Content is NativeControlHost native) native.IsVisible = false;
        _body.Content = null;
        _ = Task.Run(() => { lock (_gate) _book.Dispose(); });
    }
}
