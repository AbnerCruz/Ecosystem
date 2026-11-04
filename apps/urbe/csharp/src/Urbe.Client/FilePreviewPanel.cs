using System.IO.Compression;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// app.js openFilePreview / renderFilePreview natively: a file of the Explorer with its title, "Imagem · 120 KB ·
/// pasta/arquivo.png", and the body by
/// kind, always inside the Urbe (owner, ADD-0023): images drawn here, HTML as a page (or its source), PDF pages, audio
/// and video in the platform's content viewers (<see cref="ContentViewers"/>); text and code as text; a .zip lists
/// its entries. "Abrir" in another app stays as an extra. Read only: nothing is written to the vault.
/// </summary>
public sealed class FilePreviewPanel : Grid
{
    /// <summary>
    /// The largest file read into memory for a preview (DEC-0059: before, 32 MB, and text only up to 2 MB). Audio and
    /// video are played from the vault and never read here; a bigger file says so and opens in another app.
    /// </summary>
    public const long MaxBytes = 512L * 1024 * 1024;
    /// <summary>How much of a text is laid out at once; "Mostrar mais" adds the next part (a long log stays fluid).</summary>
    public const int TextPart = 256 * 1024;

    private readonly Border _card = new();
    private readonly TextBlock _title = new() { FontSize = 18, FontWeight = FontWeight.Bold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _meta = new() { FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
    private readonly ComboBox _choose = new() { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 10, 0, 0), IsVisible = false };
    private readonly ContentControl _body = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, Margin = new Thickness(0, 14, 0, 0) };
    private readonly Button _open = Action("Abrir em outro app", primary: true);
    private readonly StackPanel _htmlMode = new() { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 10, 0, 0), IsVisible = false };
    private bool _htmlSource;
    private bool _pdfText;
    private int _pdfPosition = 1;
    private int? _requestedPage;
    private string? _expectedHash;
    private PdfStudyView? _pdfTextView;
    private EpubStudyView? _epubView;
    private MediaStudyView? _mediaView;
    private int? _requestedPart;
    private int? _requestedTextOffset, _requestedTextLength;
    private ReaderRegion? _requestedRegion;
    public event Action<BookReaderQuote>? BookQuoteRequested;
    public event Action<ImageStudyQuote>? ImageQuoteRequested;
    public event Action<StudyImageQuestion>? VisualQuestionRequested;
    private readonly Button _ocr = new Ui.UrbeIconButton(UrbeTheme.Icons.Scan, "Reconhecer região (OCR)", 21);
    public event Action<string, string>? QuestionRequested;
    public event Action<string>? WebLinkRequested;
    public event Action<string>? AnnotationsRequested;
    public event Action<string>? ExpandRequested;
    public void SetSuspended(bool suspended) { ReaderActivity.SetActive(this, !suspended); _body.IsVisible = !suspended; }
    public async Task RetargetSourceAsync(string from, string to)
    {
        if (Current != from) return;
        if (_readingSession is { } session) _readingSession=session with { Path=to };
        CaptureReadingPosition();
        await OpenAsync(to);
    }
    public ReadingStateFile? ReadingState { get; set; }
    private ReaderSession? _readingSession;
    private VaultFileRead? _cachedRead;
    private int _cachedIndex = -1;
    private string? _cachedHash;
    private readonly StackPanel _pdfModes = new() { Orientation = Orientation.Horizontal, Spacing = 2, IsVisible = false };
    public event Action<ReaderQuote>? QuoteRequested;
    private PdfPagesView? _pdfView;
    private readonly StackPanel _pdfNavigation = new() { Orientation = Orientation.Horizontal, Spacing = 2, IsVisible = false };
    private readonly Button _pdfPrevious = new Ui.UrbeIconButton(UrbeTheme.Icons.Back, "Página anterior");
    private readonly Button _pdfNext = new Ui.UrbeIconButton(UrbeTheme.Icons.Forward, "Próxima página");
    private readonly TextBlock _pdfPage = new() { VerticalAlignment = VerticalAlignment.Center, FontSize = 13, MinWidth = 52, TextAlignment = TextAlignment.Center };
    private readonly Button _pdfTextToggle = new Ui.UrbeIconButton(UrbeTheme.Icons.TextLayer, "Texto selecionável", 21);
    private bool _readerMode;
    private readonly WrapPanel _actions = new() { Margin = new Thickness(0, 16, 0, 0) };
    private readonly Ui.UrbeIconButton _readerButton = new(UrbeTheme.Icons.Expand, "Tela cheia", 20);
    private IReadOnlyList<(string Name, string? Path)> _files = [];
    private int _version;
    private bool _choosing;

    private readonly bool _embedded;

    public FilePreviewPanel(bool embedded = false)
    {
        _embedded = embedded;
        ClipToBounds = true;
        IsVisible = false;
        Focusable = true;
        Background = UrbeTheme.Brush("#000000", .55);
        _title.Foreground = UrbeTheme.Brush(UrbeTheme.Text);
        _meta.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        var close = new Ui.UrbeIconButton(UrbeTheme.Icons.Close, "Fechar", 20);
        close.Click += async (_, _) => await TryCloseAsync();
        // "⋯": what the reader can do beyond the bar (Sumário, Anotações, another app) and the file's own actions
        var more = new Ui.UrbeIconButton(UrbeTheme.Icons.MoreUi, "Mais ações", 20);
        more.Click += (_, _) => ShowMoreMenu(more);
        _more = more;
        // top bar: ‹ back (phone) · title over a small detail line · full screen (wide) · ⋯ · ✕ (wide)
        _back.Click += async (_, _) =>
        {
            if (_body.Content is OcrStudyView ocr) { ocr.Back(); return; }
            if (!ExitReaderMode()) await TryCloseAsync();
        };
        var head = new DockPanel { MinHeight = 52 };
        _head = head;
        _close = close;
        DockPanel.SetDock(_back, Dock.Left);
        DockPanel.SetDock(close, Dock.Right);
        DockPanel.SetDock(more, Dock.Right);
        DockPanel.SetDock(_readerButton, Dock.Right);
        head.Children.Add(_back);
        head.Children.Add(close);
        head.Children.Add(more);
        head.Children.Add(_readerButton);
        _title.FontSize = 16;
        _title.FontWeight = FontWeight.SemiBold;
        _meta.FontSize = 12;
        _meta.Margin = new Thickness(0, 1, 0, 0);
        _meta.TextWrapping = TextWrapping.NoWrap;
        _meta.TextTrimming = TextTrimming.CharacterEllipsis;
        head.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0), Children = { _title, _meta } });
        _readerButton.Click += (_, _) => SetReaderMode(!_readerMode);
        _choose.SelectionChanged += async (_, _) =>
        {
            if (_choosing || _choose.SelectedIndex < 0 || !IsVisible) return;
            int selected = _choose.SelectedIndex;
            if (!await ConfirmMapExitAsync())
            {
                _choosing = true;
                _choose.SelectedIndex = Index;
                _choosing = false;
                return;
            }
            await ShowAsync(selected);
        };
        _open.Click += async (_, _) => await OpenExternallyAsync();
        var actions = _actions;
        actions.Children.Add(_open);
        var layout = new DockPanel();
        // htmlRenderBtn / htmlSourceBtn: the page or its source
        foreach (var (label, source) in new[] { ("Página", false), ("Fonte", true) })
        {
            var b = Chip(label);
            b.Click += async (_, _) =>
            {
                if (_htmlSource == source || !await ConfirmMapExitAsync()) return;
                _htmlSource = source;
                await ShowAsync(Index);
            };
            _htmlMode.Children.Add(b);
        }
        // the PDF's bar, one row of icons: ‹ 3 / 120 › · − + · text layer · selection · OCR
        var zoomIn = new Ui.UrbeIconButton(UrbeTheme.Icons.ZoomIn, "Aumentar", 21);
        zoomIn.Click += (_, _) => { if (_pdfView is { } pdf) pdf.SetZoom(Math.Min(16, pdf.Zoom * 1.25)); };
        var zoomOut = new Ui.UrbeIconButton(UrbeTheme.Icons.ZoomOut, "Diminuir", 21);
        zoomOut.Click += (_, _) => { if (_pdfView is { } pdf) pdf.SetZoom(Math.Max(.25, pdf.Zoom / 1.25)); };
        _pdfNavigation.Children.Add(_pdfPrevious);
        _pdfNavigation.Children.Add(_pdfPage);
        _pdfNavigation.Children.Add(_pdfNext);
        var gap = BarGap();
        _pdfNavigation.Children.Add(gap);
        _pdfNavigation.Children.Add(zoomOut);
        _pdfNavigation.Children.Add(zoomIn);
        _zoomTools.AddRange([gap, zoomOut, zoomIn]);
        _pdfPrevious.Click += (_, _) => _pdfView?.GoToPage(_pdfView.CurrentPage - 1);
        _pdfNext.Click += (_, _) => _pdfView?.GoToPage(_pdfView.CurrentPage + 1);
        // the page's picture or its text layer (selectable, searchable): one button that switches
        _pdfTextToggle.Click += async (_, _) => await SetPdfTextAsync(!_pdfText);
        _pdfModes.Children.Add(_pdfTextToggle);
        var select = new Ui.UrbeIconButton(UrbeTheme.Icons.Select, "Seleção", 21);
        var enable=new MenuItem{Header="Selecionar na página"};enable.Click+=async(_,_)=>
        {
            if(_pdfText) await SetPdfTextAsync(false);
            if(_pdfView is { } pdf){await pdf.EnableSelectionAsync();_meta.Text=pdf.SelectionStatus;}
        };
        var copy=new MenuItem{Header="Copiar com fonte"};copy.Click+=async(_,_)=>{if(_pdfView?.SelectedQuote() is { } quote)await Ui.TextActions.CopyAsync(this,quote.Text+"\nFonte: "+quote.Path+", p. "+quote.Page);};
        var cite=new MenuItem{Header="Adicionar seleção à nota"};cite.Click+=(_,_)=>_pdfView?.CiteSelection();
        var highlight=new MenuItem{Header="Destacar seleção"};highlight.Click+=(_,_)=>_pdfView?.CiteSelection(StudyAnnotationKind.Highlight);
        var comment=new MenuItem{Header="Comentar seleção"};comment.Click+=(_,_)=>_pdfView?.CiteSelection(StudyAnnotationKind.Comment);
        var question=new MenuItem{Header="Perguntar à IA"};question.Click+=(_,_)=>_pdfView?.CiteSelection(ask:true);
        var selectionMenu=new ContextMenu{ItemsSource=new[]{enable,copy,cite,highlight,comment,question}};
        select.Click+=(_,_)=>selectionMenu.Open(select);
        _pdfModes.Children.Add(select);
        _ocr.Click += async (_, _) => await OpenOcrAsync();
        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center,
            Children = { _pdfNavigation, _pdfModes, _ocr }
        };
        // the reader's bar sits at the bottom, where the thumb is (Kindle, Kobo, Play Books)
        _bar = new Border
        {
            Child = new ScrollViewer
            {
                Content = bar,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
            },
            Padding = new Thickness(4, 2),
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Background = UrbeTheme.Brush(UrbeTheme.Surface)
        };
        foreach (var part in new Control[] { _pdfNavigation, _pdfModes, _ocr })
            part.PropertyChanged += (_, e) => { if (e.Property == IsVisibleProperty) UpdateBar(); };
        UpdateBar();
        var top = new StackPanel { Children = { head, _choose, _htmlMode } };
        _top = top;
        if (embedded)
        {
            var expand = new Ui.UrbeIconButton(UrbeTheme.Icons.Expand, "Abrir leitor", 20);
            expand.Click += (_, _) => { if (Current is { } path) ExpandRequested?.Invoke(path); };
            bar.Children.Add(expand);
            head.IsVisible = false;
            _meta.IsVisible = false;
            actions.IsVisible = false;
            _bar.BorderThickness = default;
            _bar.Background = Brushes.Transparent;
            _body.Margin = new Thickness(0, 6, 0, 0);
            Background = Brushes.Transparent;
        }
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(actions, Dock.Bottom);
        DockPanel.SetDock(_bar, embedded ? Dock.Top : Dock.Bottom);
        layout.Children.Add(top);
        layout.Children.Add(_bar);
        layout.Children.Add(actions);
        layout.Children.Add(_body);
        // immersive reading: a tap on a page or picture hides the bars, the next one brings them back
        _body.AddHandler(PointerPressedEvent, (_, e) => _tapFrom = (e.GetPosition(this), Environment.TickCount64), Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        _body.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (_tapFrom is not { } from || _embedded || Shown is not ("viewer" or "image") || _pdfView is { } pdf && pdf.Selecting) return;
            _tapFrom = null;
            var moved = e.GetPosition(this) - from.At;
            if (Math.Abs(moved.X) > 8 || Math.Abs(moved.Y) > 8 || Environment.TickCount64 - from.Ms > 300) return;
            SetChromeHidden(!ChromeHidden);
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        _card.Child = layout;
        _card.Background = UrbeTheme.Brush(UrbeTheme.Surface);
        _card.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        _card.BorderThickness = new Thickness(1);
        _card.Padding = new Thickness(20, 14, 20, 20);
        Children.Add(_card);
        PointerPressed += async (_, e) => { if (e.Source == this) await TryCloseAsync(); };
        KeyDown += async (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            if (!ExitReaderMode()) await TryCloseAsync();
            e.Handled = true;
        };
        SizeChanged += (_, _) => Place();
        _body.SizeChanged += (_, e) => { if (_body.Content is Control content) content.Height = Math.Max(1, e.NewSize.Height); };
    }

    private readonly Button _more;
    private readonly Border _bar;
    private readonly Ui.UrbeIconButton _back = new(UrbeTheme.Icons.Back, "Voltar", 22);
    private readonly DockPanel _head = null!;
    private readonly Button _close = null!;
    private readonly StackPanel _top = null!;
    private (Point At, long Ms)? _tapFrom;

    /// <summary>
    /// Android's Back inside the reader, one step at a time: out of OCR to the source, the hidden bars back, out of full
    /// screen, and only then the reader closes.
    /// </summary>
    public void HandleBack()
    {
        if (_body.Content is OcrStudyView ocr) { ocr.Back(); return; }
        if (ChromeHidden) { SetChromeHidden(false); return; }
        if (!ExitReaderMode()) Close();
    }

    /// <summary>The bars are hidden: only the page or picture is on the screen (a tap brings them back).</summary>
    public bool ChromeHidden { get; private set; }

    public void SetChromeHidden(bool hidden)
    {
        ChromeHidden = hidden;
        _top.IsVisible = !hidden;
        UpdateBar();
    }

    private bool Phone => !_embedded && Bounds.Width < 720;

    /// <summary>The reader's bar shows only with something in it (a picture or text file has none).</summary>
    private void UpdateBar() => _bar.IsVisible = !ChromeHidden && (_pdfNavigation.IsVisible || _pdfModes.IsVisible || _ocr.IsVisible || _embedded);

    private static Control BarGap() => new Border { Width = 1, Height = 22, Margin = new Thickness(6, 0), Background = UrbeTheme.Brush(UrbeTheme.Line2), VerticalAlignment = VerticalAlignment.Center };

    /// <summary>The PDF as page pictures or as its text layer, at the same page.</summary>
    public async Task SetPdfTextAsync(bool text)
    {
        if (_pdfText == text) return;
        _pdfPosition = _pdfView?.CurrentPage ?? _pdfTextView?.CurrentPage ?? _pdfPosition;
        _pdfText = text;
        MarkTextToggle();
        await ShowAsync(Index);
    }

    private void MarkTextToggle()
    {
        _pdfTextToggle.Background = _pdfText ? UrbeTheme.Brush(UrbeTheme.Accent, .22) : Brushes.Transparent;
        _pdfTextToggle.Content = UrbeTheme.Icon(UrbeTheme.Icons.TextLayer, 21, _pdfText ? UrbeTheme.Accent : UrbeTokens.Color.TextSecondary, 1.8);
        var tip = _pdfText ? "Ver as páginas" : "Texto selecionável";
        ToolTip.SetTip(_pdfTextToggle, tip);
        Avalonia.Automation.AutomationProperties.SetName(_pdfTextToggle, tip);
    }

    private async Task OpenExternallyAsync()
    {
        if (Current is { } path && Storage is { } s && !await s.OpenExternallyAsync(path))
            _meta.Text += " · não foi possível abrir em outro app";
    }

    /// <summary>"⋯": the reader's less frequent actions, then the file's own (Renomear, Mover, Excluir).</summary>
    private void ShowMoreMenu(Control anchor)
    {
        var items = new List<MenuItem>();
        MenuItem Item(string header, Action run) { var item = new MenuItem { Header = header }; item.Click += (_, _) => run(); items.Add(item); return item; }
        if (_pdfModes.IsVisible && _pdfView is { } pdf)
        {
            Item(pdf.SinglePage ? "Rolagem contínua" : "Uma página por vez", () => pdf.SetSinglePage(!pdf.SinglePage));
            Item("Sumário", () => _ = ShowPdfNavigationAsync(false));
            Item("Links da página", () => _ = ShowPdfNavigationAsync(true));
        }
        if (Current is { } path && _generated is null)
        {
            Item("Anotações desta fonte", () => AnnotationsRequested?.Invoke(path));
            Item("Abrir em outro app", () => _ = OpenExternallyAsync());
        }
        if (_generated is { } g) Item("Ações da página", () => PageActionsRequested?.Invoke(g.Source));
        else if (Current is { } file) Item("Renomear, mover ou excluir", () => ActionsRequested?.Invoke(file));
        if (items.Count == 0) return;
        new ContextMenu { ItemsSource = items }.Open(anchor);
    }

    /// <summary>"⋯": the host shows the attachment's actions (Renomear, Mover, Excluir).</summary>
    public event Action<string>? ActionsRequested;

    /// <summary>The vault the files are read from.</summary>
    public IUrbeVaultStorage? Storage { get; set; }

    /// <summary>Native dialogs for explicit save/discard/cancel before releasing an edited map.</summary>
    public UrbeDialogs? Dialogs { get; set; }

    /// <summary>Restricted Atlas Map note navigation supplied by the Urbe's native editor.</summary>
    public Func<string, Task<bool>>? AtlasOpenNote { get; set; }

    /// <summary>The vault path of the file shown, or null.</summary>
    public string? Current => _files.Count > 0 && Index < _files.Count ? _files[Index].Path : null;

    public int Index { get; private set; }

    /// <summary>Immersive reading expands the existing viewer without recreating its document or WebView.</summary>
    public bool ReaderMode => _readerMode;

    public void EnterReaderMode() => SetReaderMode(true);

    /// <summary>Consumes Back when reading expanded; the next Back may close the preview.</summary>
    public bool ExitReaderMode()
    {
        if (!_readerMode) return false;
        SetReaderMode(false);
        return true;
    }

    private void SetReaderMode(bool expanded)
    {
        if (!IsVisible || _readerMode == expanded) return;
        _readerMode = expanded;
        _readerButton.Content = UrbeTheme.Icon(expanded ? UrbeTheme.Icons.Shrink : UrbeTheme.Icons.Expand, 20, UrbeTokens.Color.TextSecondary, 1.8);
        ToolTip.SetTip(_readerButton, expanded ? "Sair da tela cheia" : "Tela cheia");
        Avalonia.Automation.AutomationProperties.SetName(_readerButton, expanded ? "Sair da tela cheia" : "Tela cheia");
        _meta.IsVisible = !expanded;
        _choose.IsVisible = !expanded && _files.Count > 1;
        _htmlMode.IsVisible = !expanded && (_generated is not null ||
            (Current is { } p && VaultFileKinds.Classify(p) == VaultFileKinds.Html));
        _actions.IsVisible = !expanded;
        _more.IsVisible = !expanded && (Current is not null || _generated is not null);
        Place();
    }

    /// <summary>What the body shows now: "image", "text", "zip", "viewer" (a content viewer) or "none" (tests).</summary>
    public string Shown { get; private set; } = "none";

    /// <summary>The text of the body when it is text or a listing (tests).</summary>
    public string? ShownText { get; private set; }

    /// <summary>One file of the vault by its path (the Explorer).</summary>
    public Task OpenAsync(string path)
    {
        _requestedPage = null;
        _requestedPart = null; _requestedTextOffset=_requestedTextLength=null; _requestedRegion=null;
        _expectedHash = null;
        return OpenAsync([(path[(path.LastIndexOf('/') + 1)..], path)], 0);
    }

    public async Task OpenPdfReferenceAsync(string path, int page, string? sha256 = null, ReaderRegion? region = null, int? textOffset = null, int? textLength = null)
    {
        _requestedPage = page; _requestedRegion = region; _requestedTextOffset=textOffset;_requestedTextLength=textLength;
        if(textOffset is not null)_pdfText=true;
        _expectedHash = sha256;
        await OpenAsync([(path[(path.LastIndexOf('/') + 1)..], path)], 0);
    }

    public Task OpenPartReferenceAsync(string path, PartSourceReference reference)
    {
        _requestedPart = reference.Index; _requestedRegion=reference.Region;
        _requestedTextOffset = reference.TextOffset; _requestedTextLength = reference.TextLength;
        _expectedHash = reference.Sha256;
        return OpenAsync([(path[(path.LastIndexOf('/') + 1)..], path)], 0);
    }

    /// <summary>
    /// A page of the vault (Páginas): the HTML the page engine wrote for <paramref name="source"/> (a .page.json), shown
    /// as a page or as its source like any HTML file; "⋯" offers the page's actions. Nothing is written to the vault.
    /// </summary>
    public async Task OpenGeneratedAsync(string title, string source, byte[] html)
    {
        if (IsVisible && !await ConfirmMapExitAsync()) return;
        ExitReaderMode();
        _cachedRead = null; _cachedIndex = -1; _cachedHash = null;
        _generated = (source, html);
        _files = [(title, null)];
        _htmlSource = false;
        _choose.IsVisible = false;
        IsVisible = true;
        Place();
        await ShowAsync(0);
        Focus();
    }

    private (string Source, byte[] Html)? _generated;

    /// <summary>The .page.json whose HTML is shown, or null (tests).</summary>
    public string? GeneratedFrom => _generated?.Source;

    /// <summary>"⋯" on a generated page: the host shows the page's actions.</summary>
    public event Action<string>? PageActionsRequested;

    private async Task OpenAsync(IReadOnlyList<(string Name, string? Path)> files, int index)
    {
        if (IsVisible && !await ConfirmMapExitAsync()) return;
        ExitReaderMode();
        _cachedRead = null; _cachedIndex = -1; _cachedHash = null;
        _generated = null;
        _pdfText = _requestedTextOffset is not null && _requestedPage is not null;
        _pdfPosition = _requestedPage ?? 1;
        _files = files;
        _htmlSource = false;
        _choosing = true;
        _choose.ItemsSource = files.Select(f => f.Name).ToList();
        _choosing = false;
        _choose.IsVisible = files.Count > 1;
        IsVisible = true;
        Place();
        await ShowAsync(Math.Clamp(index, 0, Math.Max(0, files.Count - 1)));
        Focus();
    }

    private bool _closingMap;

    /// <summary>
    /// Before losing the WebView the user explicitly saves, discards or cancels. A failed save
    /// never closes the panel or reports success; the current map remains loaded and dirty.
    /// </summary>
    public async Task<bool> ConfirmMapExitAsync()
    {
        if (_closingMap) return false;
        if (_body.Content is not IAtlasMapPendingEdits map) return true;
        try
        {
            _closingMap = true;
            if (!await map.HasPendingEditsAsync()) return true;
            if (Dialogs is null) return false; // no native confirmation surface: never silently discard
            int? choice = await Dialogs.ChooseAsync("Alterações não salvas no mapa",
            [
                new DialogItem("Salvar e continuar", Detail: "Gravar os lugares no HTML do vault"),
                new DialogItem("Descartar alterações", Detail: "Perder as modificações desta sessão", Danger: true),
                new DialogItem("Cancelar", Detail: "Continuar editando o mapa")
            ], "Este mapa contém lugares ou descrições ainda não gravados.");
            if (choice == 1) return true;
            if (choice == 0)
            {
                if (await map.SavePendingEditsAsync() && !await map.HasPendingEditsAsync()) return true;
                await Dialogs.AlertAsync("Mapa não salvo", "A gravação não foi confirmada. Suas alterações continuam abertas no mapa.");
            }
            return false;
        }
        catch (Exception ex) when (ex is ObjectDisposedException or TimeoutException or InvalidOperationException or IOException)
        {
            if (Dialogs is not null)
                await Dialogs.AlertAsync("Mapa não salvo", "Não foi possível verificar a gravação: " + ex.Message);
            return false;
        }
        finally { _closingMap = false; }
    }

    public async Task<bool> TryCloseAsync()
    {
        if (!await ConfirmMapExitAsync()) return false;
        if (_epubView is { } reader) await reader.CaptureLocationAsync();
        CaptureReadingPosition();
        if (ReadingState is { } state) await state.FlushAsync();
        CloseCore();
        return true;
    }

    /// <summary>Existing synchronous callers retain the fast path when no WebView/map is pending.</summary>
    public void Close()
    {
        if (_body.Content is IAtlasMapPendingEdits) { _ = TryCloseAsync(); return; }
        CloseCore();
    }

    private void CloseCore()
    {
        CaptureReadingPosition();
        ExitReaderMode();
        _pdfView = null; _mediaView=null;
        _pdfNavigation.IsVisible = false;
        _pdfTextView?.Dispose();
        _epubView?.Dispose(); _epubView = null;
        _pdfTextView = null;
        _requestedPage = null;
        _expectedHash = null;
        _version++;
        _cachedRead = null; _cachedIndex = -1; _cachedHash = null;
        IsVisible = false;
        if (_body.Content is NativeControlHost native) native.IsVisible = false;
        _body.Content = null;
        Shown = "none";
        ShownText = null;
    }

    private void Place()
    {
        if (_embedded)
        {
            _card.Width = _card.Height = double.NaN;
            _card.MaxHeight = double.PositiveInfinity;
            _card.Padding = default;
            _card.BorderThickness = default;
            _card.HorizontalAlignment = HorizontalAlignment.Stretch;
            _card.VerticalAlignment = VerticalAlignment.Stretch;
            if (_body.Content is Control content) content.Height = ViewerHeight();
            return;
        }
        bool wide = Bounds.Width >= 720;
        // phone: a reader is the whole screen, never a sheet over the note (the bars are what is thin)
        _back.IsVisible = !wide;
        _close.IsVisible = wide;
        _readerButton.IsVisible = wide;
        if (_readerMode || !wide)
        {
            _card.Width = double.NaN;
            _card.Height = double.NaN;
            _card.MaxHeight = double.PositiveInfinity;
            _card.HorizontalAlignment = HorizontalAlignment.Stretch;
            _card.VerticalAlignment = VerticalAlignment.Stretch;
            _card.CornerRadius = new CornerRadius(0);
        }
        else
        {
            _card.Width = wide ? Math.Min(760, Bounds.Width - 48) : double.NaN;
            _card.Height = double.NaN;
            _card.MaxHeight = wide ? Math.Max(320, Bounds.Height - 64) : Math.Max(320, Bounds.Height * .9);
            _card.HorizontalAlignment = wide ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
            _card.VerticalAlignment = wide ? VerticalAlignment.Center : VerticalAlignment.Bottom;
            _card.CornerRadius = wide ? new CornerRadius(16) : new CornerRadius(20, 20, 0, 0);
        }
        // phone: edge to edge, the page's own colour behind it; zoom is the pinch, so −/+ leave the bar
        _card.Padding = !wide ? new Thickness(0) : _readerMode ? new Thickness(8) : new Thickness(20, 14, 20, 20);
        _card.BorderThickness = new Thickness(wide ? 1 : 0);
        _card.Background = UrbeTheme.Brush(wide ? UrbeTheme.Surface : UrbeTheme.Bg);
        _head.Margin = wide ? default : new Thickness(4, 0, 4, 0);
        foreach (var zoom in _zoomTools) zoom.IsVisible = wide;
        if (Shown is "viewer" or "text" or "image" && _body.Content is Control shown)
            shown.Height = ViewerHeight();
    }

    private readonly List<Control> _zoomTools = [];

    private async Task ShowAsync(int index)
    {
        if (ChromeHidden) SetChromeHidden(false);
        CaptureReadingPosition();
        int version = ++_version;
        _mediaView=null;
        _pdfView = null; _mediaView=null;
        _pdfNavigation.IsVisible = false;
        _pdfTextView?.Dispose();
        _epubView?.Dispose(); _epubView = null;
        _pdfTextView = null;
        Index = index;
        _choosing = true;
        _choose.SelectedIndex = index;
        _choosing = false;
        var (name, path) = _files[index];
        var kind = _generated is not null ? VaultFileKinds.Html : VaultFileKinds.Classify(name);
        _pdfModes.IsVisible = kind == VaultFileKinds.Pdf;
        MarkTextToggle();
        _ocr.IsVisible = kind is VaultFileKinds.Pdf or VaultFileKinds.Image;
        _title.Text = name;
        _meta.Text = VaultFileKinds.Label(kind) + (path is not null ? " · " + path : "");
        _open.IsVisible = false;
        _more.IsVisible = path is not null || _generated is not null;
        _htmlMode.IsVisible = kind == VaultFileKinds.Html;
        for (int i = 0; i < _htmlMode.Children.Count; i++)
            if (_htmlMode.Children[i] is Button chip) Mark(chip, (i == 1) == _htmlSource);
        Shown = "none";
        ShownText = null;
        // A previous native WebView can outlive a content swap for a frame on Android
        // and cover the Avalonia source viewer. Hide it before detaching it.
        if (_body.Content is NativeControlHost oldNativeViewer) oldNativeViewer.IsVisible = false;
        _body.Content = Empty("Carregando do dispositivo…");
        // audio and video are played from the vault by the platform: only their size is read here
        // Files with no in-app renderer still have metadata and an explicit external fallback.
        // Do not allocate a giant Office/EPUB container merely to say its preview is unsupported.
        bool metadataOnly = kind is VaultFileKinds.Audio or VaultFileKinds.Video ||
            kind is VaultFileKinds.Slides or VaultFileKinds.Document && Path.GetExtension(name).ToLowerInvariant() is not (".pptx" or ".odp" or ".docx" or ".odt");
        VaultFileRead? read;
        try
        {
            read = _cachedIndex == index && _cachedRead is not null ? _cachedRead : _generated is { } g ? new VaultFileRead(g.Html.Length, g.Html)
                : path is not null && Storage is { } s ? await s.ReadFileAsync(path, metadataOnly ? 0 : MaxBytes) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            if (version == _version) _body.Content = Empty("Não foi possível ler o arquivo", "Confira se o arquivo continua disponível e se a pasta está acessível.");
            return;
        }
        if (version != _version) return;
        if (_cachedIndex != index) _cachedHash = null;
        _cachedRead = read; _cachedIndex = index;
        if (read is null)
        {
            _body.Content = Empty("Arquivo não encontrado", "O item aparece no Explorer, mas não foi encontrado na pasta do vault.");
            return;
        }
        _meta.Text = (_generated is { } gen ? "Página · " + VaultFileKinds.FormatBytes(read.Size) + " · " + gen.Source
            : VaultFileKinds.Label(kind) + " · " + VaultFileKinds.FormatBytes(read.Size) + (path is not null ? " · " + path : ""));
        // "Abrir em outro app" is in "⋯"; the big button only when the Urbe cannot show the file itself
        _open.IsVisible = false;
        var bytes = read.Bytes;
        if ((bytes is not null || kind is VaultFileKinds.Audio or VaultFileKinds.Video) && Storage?.AtlasVaultSessionKey is { } vaultIdentity && path is not null && ReadingState is { } state)
        {
            string? revision=_cachedHash??(bytes is not null?await Task.Run(()=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant()):await Storage.ReaderRevisionAsync(path));
            if(version!=_version)return;
            _cachedHash=revision;
            if(revision is not null)
            {
                string vaultKey=ReaderSessionCatalog.VaultKey(vaultIdentity);
                _readingSession=state.Restore(vaultKey,path,revision)??new ReaderSession(vaultKey,path,revision,kind,new(1),new(),DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                state.Remember(_readingSession with{Updated=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()});
                if(_requestedPage is null)_pdfPosition=_readingSession.Location.Part;
                if(_requestedPart is null)_requestedPart=kind is VaultFileKinds.Audio or VaultFileKinds.Video?_readingSession.Location.Part-1:_readingSession.Location.Part;
            }
            else _readingSession=null;
        }
        else _readingSession=null;
        if (_expectedHash is { } mediaHash && bytes is null && kind is VaultFileKinds.Audio or VaultFileKinds.Video && Storage is { } mediaStorage && path is not null)
        {
            string? actual=_cachedHash??await mediaStorage.ReaderRevisionAsync(path);
            if(version!=_version)return;
            _expectedHash=null;
            if(!string.Equals(actual,mediaHash,StringComparison.OrdinalIgnoreCase))
            {_body.Content=Empty("Fonte alterada","Não foi possível confirmar a versão desta mídia. Confira o arquivo antes de reutilizar o instante.");return;}
        }
        if (_expectedHash is { } expected && bytes is not null)
        {
            var hash = _cachedHash ?? await Task.Run(() => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));
            if (version != _version) return;
            _expectedHash = null;
            _requestedPage = null;
            if (!string.Equals(hash, expected, StringComparison.OrdinalIgnoreCase))
            {
                _pdfModes.IsVisible = false;
                _pdfPosition = 1; _requestedPart = _requestedTextOffset = _requestedTextLength = null; _requestedRegion = null;
                _body.Content = Empty("Fonte alterada", "Este arquivo mudou desde a citação. Abra-o pelo Explorer para conferir a referência; o trecho não foi associado automaticamente à nova versão.");
                return;
            }
        }
        _requestedPage = null;
        if ((kind is VaultFileKinds.Epub or VaultFileKinds.Comic || Path.GetExtension(name).ToLowerInvariant() is ".pptx" or ".odp" or ".docx" or ".odt") && bytes is not null)
        {
            try
            {
                IReaderPublication book = await Task.Run<IReaderPublication>(() => name.EndsWith(".epub", StringComparison.OrdinalIgnoreCase) ? new EpubReadingDocument(bytes) : new OfficeReadingDocument(name, bytes));
                if (version != _version) { book.Dispose(); return; }
                var epub = new EpubStudyView(path ?? name, book, _embedded) { Height = ViewerHeight() };
                _epubView = epub;
                epub.QuoteRequested += quote => BookQuoteRequested?.Invoke(quote);
                epub.AskRequested += quote => QuestionRequested?.Invoke(quote.Path + " · " + quote.Unit + " " + quote.Index, quote.Text);
                epub.ExternalLinkRequested += url => WebLinkRequested?.Invoke(url);
                epub.PositionChanged += chapter => RememberReadingPosition(chapter + 1, fontSize: epub.FontSizePreference);
                _body.Content = epub; Shown = "viewer";
                if (_readingSession is { } saved) {epub.RestoreAppearance(saved.Preferences.Theme,saved.Preferences.Margin); await epub.SetTypographyAsync(saved.Preferences.FontSize);}
                epub.RestoreLineHeight(_readingSession?.Preferences.LineHeight ?? 1.6);
                await epub.GoToChapterAsync((_requestedPart ?? 1) - 1, _requestedTextOffset ?? _readingSession?.Location.Offset ?? 0, _requestedTextLength ?? 0, _readingSession?.Location.Scroll ?? 0);
                _requestedTextOffset = _requestedTextLength = null;
                _requestedPart = null;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            { if (version == _version) { _body.Content = Empty("Não foi possível abrir o arquivo de leitura", ex.Message); _open.IsVisible = true; } }
            return;
        }
        if (kind == VaultFileKinds.Image && bytes is not null && Image(bytes) is { } image)
        {
            if(_requestedRegion is { } region)
            {
                var ocrSource=new OcrStudyView(path??name,null,_cachedHash??Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(),bytes,region){Height=ViewerHeight(),ShowsBack=!Phone};
                ocrSource.QuoteRequested+=quote=>ImageQuoteRequested?.Invoke(quote);ocrSource.VisualQuestionRequested+=question=>VisualQuestionRequested?.Invoke(question);ocrSource.CloseRequested+=async()=>await ShowAsync(Index);_body.Content=ocrSource;_requestedRegion=null;Shown="image";return;
            }
            image.Height = ViewerHeight();
            _body.Content = image;
            Shown = "image";
            return;
        }
        if (kind == VaultFileKinds.Zip && bytes is not null && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
                var names = zip.Entries.Where(e => !e.FullName.EndsWith('/')).Select(e => e.FullName).ToList();
                var text = names.Count > 0 ? string.Join("\n", names) : "Arquivo compactado vazio";
                ShowText(text, "zip");
            }
            catch (InvalidDataException) { _body.Content = Empty("Arquivo compactado", name); }
            return;
        }
        // HTML as a page, PDF, audio and video: the content viewer of the platform, inside the panel (ADD-0023)
        if (kind is VaultFileKinds.Pdf or VaultFileKinds.Audio or VaultFileKinds.Video || (kind == VaultFileKinds.Html && !_htmlSource))
        {
            Control? viewer = null;
            try { if (kind != VaultFileKinds.Pdf || !_pdfText) viewer = await ContentViewers.CreateAsync(new ContentRequest(Storage, path ?? _generated?.Source ?? name, name, kind, bytes) { AtlasOpenNote = path is null ? null : AtlasOpenNote }); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { viewer = null; }
            if (version != _version) { (viewer as IDisposable)?.Dispose(); return; }
            if (viewer is not null)
            {
                if (kind is VaultFileKinds.Audio or VaultFileKinds.Video && Storage is { } mediaVault)
                {
                    var media=new MediaStudyView(path??name,mediaVault,viewer,_requestedPart,_cachedHash);
                    media.QuoteRequested+=quote=>BookQuoteRequested?.Invoke(quote);media.PositionChanged+=second=>RememberReadingPosition(second+1);_mediaView=media;viewer=media;_requestedPart=null;
                }
                viewer.Height = ViewerHeight();
                _body.Content = viewer;
                Shown = "viewer";
                if (viewer is PdfPagesView pdf)
                {
                    _pdfView = pdf;
                    if(bytes is not null)pdf.ConfigureStudy(path??name,bytes,_cachedHash??Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant());
                    pdf.QuoteRequested+=quote=>QuoteRequested?.Invoke(quote);pdf.AskRequested+=quote=>QuestionRequested?.Invoke(quote.Path+" · página "+quote.Page,quote.Text);
                    _pdfNavigation.IsVisible = true;
                    pdf.PageChanged += _ => { if (ReferenceEquals(_pdfView, pdf)) UpdatePdfNavigation(); };
                    pdf.PageChanged += page => RememberReadingPosition(page);
                    UpdatePdfNavigation();
                    pdf.Loaded += (_, _) => { pdf.SetZoom(_readingSession?.Preferences.Zoom ?? 1); pdf.GoToPage(_pdfPosition); if (_requestedRegion is { } region) { pdf.FocusRegion(_pdfPosition,region); _requestedRegion=null; } };
                }
                return;
            }
            if (kind == VaultFileKinds.Pdf && bytes is not null)
            {
                var study = new PdfStudyView(path ?? name, bytes) { Height = ViewerHeight() };
                _pdfTextView = study;
                study.QuoteRequested += quote => QuoteRequested?.Invoke(quote);
                study.AskRequested += quote => QuestionRequested?.Invoke(quote.Path + " · página " + quote.Page, quote.Text);
                study.PageChanged += page => _pdfPosition = page;
                study.Failed += () => _open.IsVisible = _generated is null;
                study.PageChanged += page => RememberReadingPosition(page);
                _body.Content = study;
                Shown = "text";
                if(_requestedTextOffset is { } offset) { await study.GoToTextAsync(_pdfPosition,offset,_requestedTextLength??0);_requestedTextOffset=_requestedTextLength=null; }
                else await study.GoToPageAsync(_pdfPosition);
                return;
            }
        }
        bool textual = kind is VaultFileKinds.Html or VaultFileKinds.Other || name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
        if (textual && bytes is not null && Text(bytes) is { } source)
        {
            ShowText(source, "text");
            return;
        }
        _open.IsVisible = _generated is null;
        if (bytes is null && read.Size > MaxBytes)
        {
            _body.Content = Empty(name, "O arquivo tem " + VaultFileKinds.FormatBytes(read.Size) + ", mais do que este aparelho abre aqui de uma vez. Use Abrir em outro app.");
            return;
        }
        _body.Content = Empty(name, kind is VaultFileKinds.Pdf or VaultFileKinds.Audio or VaultFileKinds.Video
            ? "Este aparelho ainda não tem o visualizador deste formato dentro do Urbe."
            : kind is VaultFileKinds.Epub or VaultFileKinds.Slides or VaultFileKinds.Document
                ? "O leitor interno deste formato está em desenvolvimento. Use Abrir em outro app sem modificar o original."
                : "Sem visualização nativa para este formato.");
    }

    private void UpdatePdfNavigation()
    {
        if (_pdfView is not { } pdf) return;
        _pdfPage.Text = pdf.CurrentPage + " / " + pdf.PageCount;
        _pdfPrevious.IsEnabled = pdf.CurrentPage > 1;
        _pdfNext.IsEnabled = pdf.CurrentPage < pdf.PageCount;
    }

    private async Task ShowPdfNavigationAsync(bool links)
    {
        if (_cachedRead?.Bytes is not { } bytes || Dialogs is not { } dialogs || dialogs.IsOpen) return;
        int version = _version;
        try
        {
            int page = _pdfView?.CurrentPage ?? _pdfTextView?.CurrentPage ?? _pdfPosition;
            var entries = await Task.Run(() => links ? Urbe.Core.Assistant.DocumentNavigation.Links(bytes, page)
                : Urbe.Core.Assistant.DocumentNavigation.Outline(bytes).Select(e => new Urbe.Core.Assistant.DocumentNavigationLink(new string(' ', Math.Clamp(e.Level,0,10) * 2) + e.Title,e.Page,null)).ToArray());
            if (version != _version) return;
            if (entries.Count == 0) { _meta.Text = links ? "Esta página não possui links navegáveis." : "Este PDF não possui sumário navegável."; return; }
            var choice = await dialogs.ChooseAsync(links ? "Links da página" : "Sumário do PDF", entries.Select(e => new DialogItem(string.IsNullOrWhiteSpace(e.Text) ? e.Url ?? "Página " + e.Page : e.Text)).ToArray());
            if (version != _version || choice is not { } index) return;
            var target = entries[index];
            if (target.Page is { } destination) { _pdfView?.GoToPage(destination); if (_pdfTextView is { } text) await text.GoToPageAsync(destination); }
            else if (target.Url is { } url) WebLinkRequested?.Invoke(url);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { _meta.Text = "A navegação deste PDF não pôde ser lida: " + ex.Message; }
    }
    private async Task OpenOcrAsync()
    {
        int version = _version;
        if (Current is not { } path || _cachedRead?.Bytes is not { } bytes) return;
        _ocr.IsEnabled = false;
        try
        {
            CaptureReadingPosition();
            int? page = path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? _pdfView?.CurrentPage ?? _pdfTextView?.CurrentPage ?? _pdfPosition : null;
            byte[] image;
            if (page is { } number)
            {
                if (_pdfView is { } visual) image = await visual.CapturePageAsync(number);
                else
                {
                    using var document = await PortablePdfDocument.OpenAsync(bytes, default);
                    image = await Task.Run(() => { var p = document.Render(number - 1, 2000); using var bmp = Pixels.ToBitmap(p.Rgba, p.Width, p.Height); using var output = new MemoryStream(); bmp.Save(output, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default); return output.ToArray(); });
                }
            }
            else image = bytes;
            string hash = _cachedHash ?? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
            if (version != _version) return;
            var ocr = new OcrStudyView(path, page, hash, image) { Height = ViewerHeight(), ShowsBack = !Phone };
            ocr.VisualQuestionRequested+=question=>VisualQuestionRequested?.Invoke(question);
            ocr.QuoteRequested += quote => ImageQuoteRequested?.Invoke(quote);
            ocr.CloseRequested += async () => { ocr.Dispose(); await ShowAsync(Index); };
            _body.Content = ocr; _pdfView = null; _pdfTextView = null;
            _ocr.IsVisible = _pdfNavigation.IsVisible = _pdfModes.IsVisible = false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { _meta.Text = "Não foi possível preparar a região: " + ex.Message; }
        finally { _ocr.IsEnabled = true; }
    }

    private void RememberReadingPosition(int part, double? fontSize = null)
    {
        if (_readingSession is not { } session || ReadingState is null) return;
        var location = new ReaderLocation(part, _epubView?.CurrentOffset ?? 0, _epubView?.CurrentScroll ?? 0);
        var preferences = session.Preferences with { FontSize = fontSize ?? session.Preferences.FontSize, LineHeight = _epubView?.LineHeightPreference ?? session.Preferences.LineHeight, Zoom = _pdfView?.Zoom ?? session.Preferences.Zoom, Theme=_epubView?.ThemePreference??session.Preferences.Theme, Margin=_epubView?.MarginPreference??session.Preferences.Margin };
        if (session.Location == location && session.Preferences == preferences) return;
        _readingSession = session with { Location = location, Preferences = preferences, Updated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        ReadingState.Remember(_readingSession);
    }

    private void CaptureReadingPosition()
    {
        if (_epubView is { } book) RememberReadingPosition(book.CurrentChapter + 1, book.FontSizePreference);
        else if (_pdfView is { } pdf) RememberReadingPosition(pdf.CurrentPage);
        else if (_pdfTextView is { } text) RememberReadingPosition(text.CurrentPage);
        else if(_mediaView is { } media)RememberReadingPosition(media.CurrentSecond+1);
    }

    /// <summary>The height of a content viewer: a native view needs one, and it fills what the card leaves.</summary>
    private double ViewerHeight() => _body.Bounds.Height > 1 ? _body.Bounds.Height : _embedded ? Math.Max(1, Bounds.Height - (_pdfModes.IsVisible ? 40 : 0) - (_pdfNavigation.IsVisible ? 48 : 0) - (_htmlMode.IsVisible ? 44 : 0)) : _readerMode
        ? Math.Max(1, Bounds.Height - 96)
        : Bounds.Width >= 720 ? Math.Max(260, Bounds.Height - 280) : Math.Max(260, Bounds.Height * .9 - 250);

    private static Button Chip(string text) => new()
    {
        Content = text, Height = 34, Padding = new Thickness(14, 0), CornerRadius = new CornerRadius(17), FontSize = 13, FontWeight = FontWeight.SemiBold,
        VerticalContentAlignment = VerticalAlignment.Center, BorderThickness = new Thickness(1)
    };

    private static void Mark(Button chip, bool on)
    {
        chip.Background = UrbeTheme.Brush(on ? UrbeTheme.Accent : UrbeTheme.Surface2);
        chip.Foreground = UrbeTheme.Brush(on ? UrbeTheme.AccentInk : UrbeTheme.Text2);
        chip.BorderBrush = UrbeTheme.Brush(on ? UrbeTheme.Accent : UrbeTheme.Line2);
    }

    private void ShowText(string text, string shown)
    {
        var parts = new StackPanel();
        SelectableTextBlock Part(string piece) => new()
        {
            Text = piece, FontFamily = new FontFamily("Consolas, Menlo, monospace"), FontSize = 13, LineHeight = 19,
            Foreground = UrbeTheme.Brush(UrbeTheme.Text2), Padding = new Thickness(12, 0)
        };
        // a long text is laid out in parts, each one ending at a line, so opening it never freezes the screen
        int shownUpTo = 0;
        Button? more = null;
        void Next()
        {
            int end = Math.Min(text.Length, shownUpTo + TextPart);
            if (end < text.Length && text.LastIndexOf('\n', end - 1, end - shownUpTo) is var nl and >= 0 && nl + 1 > shownUpTo) end = nl + 1;
            if (end < text.Length && char.IsHighSurrogate(text[end - 1])) end++;
            parts.Children.Insert(more is null ? parts.Children.Count : parts.Children.Count - 1, Part(text[shownUpTo..end]));
            shownUpTo = end;
            var left = text.Length - shownUpTo;
            if (left == 0) { if (more is not null) parts.Children.Remove(more); return; }
            if (more is null)
            {
                more = new Button { Margin = new Thickness(12, 8), HorizontalAlignment = HorizontalAlignment.Left };
                more.Click += (_, _) => Next();
                parts.Children.Add(more);
            }
            more.Content = "Mostrar mais (" + VaultFileKinds.FormatBytes(Encoding.UTF8.GetByteCount(text.AsSpan(shownUpTo))) + " restantes)";
        }
        Next();
        _body.Content = new ScrollViewer
        {
            Height = ViewerHeight(),
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = new Border { Padding = new Thickness(0, 12), Child = parts },
            Background = UrbeTheme.Brush(UrbeTheme.Bg)
        };
        Shown = shown;
        ShownText = text;
    }

    /// <summary>The widest a picture is decoded for the preview (it zooms in from there): an 8000-pixel scan would take hundreds of MB.</summary>
    private const int MaxPictureWidth = 4096;

    private static Control? Image(byte[] bytes)
    {
        try
        {
            // a very large picture (a panorama, a scan) is decoded at a size the screen can still zoom into, never refused
            Bitmap Decode()
            {
                using var input = new MemoryStream(bytes, writable: false);
                return ImageHeader.Width(bytes) is > MaxPictureWidth ? Bitmap.DecodeToWidth(input, MaxPictureWidth) : new Bitmap(input);
            }
            return new Ui.ZoomImageView(Decode);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or IOException) { return null; }
    }

    /// <summary>The file as text: UTF-8 (with or without BOM); null when it has NUL bytes or is not valid UTF-8.</summary>
    public static string? Text(byte[] bytes)
    {
        if (Array.IndexOf(bytes, (byte)0) >= 0) return null;
        try { return new UTF8Encoding(false, true).GetString(bytes).TrimStart('﻿'); }
        catch (DecoderFallbackException) { return null; }
    }

    private static Control Empty(string title, string? message = null)
    {
        var box = new StackPanel { Spacing = 6, Margin = new Thickness(0, 24), HorizontalAlignment = HorizontalAlignment.Center };
        box.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.Bold, FontSize = 15, Foreground = UrbeTheme.Brush(UrbeTheme.Text), HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
        if (message is not null)
            box.Children.Add(new TextBlock { Text = message, FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 });
        return box;
    }

    private static Button Action(string text, bool primary) => new()
    {
        Content = text, Height = 48, CornerRadius = new CornerRadius(12), FontSize = 15, FontWeight = FontWeight.SemiBold,
        HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
        Background = UrbeTheme.Brush(primary ? UrbeTheme.Accent : UrbeTheme.Surface2),
        Foreground = UrbeTheme.Brush(primary ? UrbeTheme.AccentInk : UrbeTheme.Text),
        BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(primary ? 0 : 1)
    };
}
