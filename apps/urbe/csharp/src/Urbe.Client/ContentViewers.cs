using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Urbe.Client;

public enum ReaderContentEngine { None, Presentation, Ocr, Document }

/// <summary>
/// What a content viewer is asked to show (ADD-0023/DEC-0046): one file of the vault, by its path and kind
/// (<see cref="Core.VaultFileKinds"/>), with its bytes when they were read, and the vault it comes from so a platform
/// can reach the file itself (a large video is played from the vault, not loaded into memory).
/// </summary>
public sealed record ContentRequest(IUrbeVaultStorage? Storage, string Path, string Name, string Kind, byte[]? Bytes)
{
    /// <summary>
    /// The studio's preview: the page's messages ({urbePage:"select"|"text",…} as JSON, what the 1.8.4 preview posts to its parent, and "comment" from a 💬, UC-49)
    /// are handed here, on the UI thread; null for every other viewer (isolated, no way back to the app).
    /// </summary>
    public Action<string>? Messages { get; init; }

    /// <summary>Only a validated Atlas Map V1 can request opening an existing vault note. No file or vault access is delegated.</summary>
    public Func<string, Task<bool>>? AtlasOpenNote { get; init; }
    /// <summary>Read-only EPUB/slide navigation. Only the reader's known source parts may be opened.</summary>
    public Action<string>? ReaderNavigate { get; init; }
    public ReaderContentEngine ReaderEngine { get; init; }
}

/// <summary>
/// Printing is a platform operation, but page/paper semantics come from the page document.
/// Android WebView printing does not honor CSS @page size; the print service needs the paper explicitly.
/// </summary>
public sealed record PagePrintSettings(bool IsBook, string Paper)
{
    public static PagePrintSettings FromLayout(System.Text.Json.Nodes.JsonObject layout)
    {
        if ((string?)layout["format"] != "book") return new(false, "a4");
        var paper = (string?)layout["pageSize"] ?? "a5";
        return new(true, paper is "a4" or "a5" or "letter" or "6x9" or "pocket" ? paper : "a5");
    }
}

/// <summary>
/// The content viewers inside the Urbe (owner, ADD-0023: "dá um jeito da visualização aparecer no próprio Urbe,
/// muitos painéis assim serão necessários"). The interface stays native (ADR-0032); a viewer is content inside a
/// native panel: HTML and media in the system's web view and player, PDF pages drawn as images. Each platform head
/// registers what it can show; what it cannot is left to the panel's own fallback. Reused by every panel that shows
/// HTML products (pages, compositions, artifacts).
/// </summary>
public static class ContentViewers
{
    /// <summary>The platform's viewer for a request, or null when it has none for that kind.</summary>
    public static Func<ContentRequest, CancellationToken, Task<Control?>>? Factory { get; set; }

    /// <summary>The platform's PDF document reader (the pages are drawn by <see cref="PdfPagesView"/>), or null.</summary>
    public static Func<ContentRequest, CancellationToken, Task<IPdfDocument?>>? PdfReader { get; set; }

    /// <summary>
    /// printPdf(): the platform prints this HTML (the system's print dialog, where "Salvar como PDF" is one of the
    /// printers), named after the page; null when the platform cannot print.
    /// </summary>
    public static Func<string, string, PagePrintSettings, Task>? Printer { get; set; }

    /// <summary>A viewer for <paramref name="request"/>: a PDF by the platform's reader, anything else by its factory.</summary>
    public static async Task<Control?> CreateAsync(ContentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Kind == Core.VaultFileKinds.Pdf && PdfReader is { } reader && await reader(request, cancellationToken) is { } pdf)
            return new PdfPagesView(pdf);
        if (request.Kind == Core.VaultFileKinds.Pdf && request.Bytes is { } bytes)
            return new PdfPagesView(await PortablePdfDocument.OpenAsync(bytes, cancellationToken));
        return Factory is { } factory ? await factory(request, cancellationToken) : null;
    }
}

/// <summary>
/// Only approved Atlas Map viewers expose pending-edit checks. Closing a native panel or navigating
/// away must await this boundary before releasing its WebView. Other HTML viewers do not implement it.
/// </summary>
public interface IAtlasMapPendingEdits
{
    Task<bool> HasPendingEditsAsync();
    Task<bool> SavePendingEditsAsync();
}

/// <summary>An HTML viewer that can show new HTML in place (the studio's preview: no new browser for every change).</summary>
public interface IHtmlReloadable
{
    void Reload(string html);
    /// <summary>A message posted to the page (window.postMessage), as the 1.8.4 posted to the preview's iframe.</summary>
    void Post(string json);
}

/// <summary>A PDF opened by the platform: its pages, their size, and each page drawn as RGBA at a given width.</summary>
public interface IPdfDocument : IDisposable
{
    int PageCount { get; }

    /// <summary>The page's size in points.</summary>
    Size PageSize(int index);

    /// <summary>The page drawn on white, <paramref name="width"/> pixels wide (RGBA, height in proportion).</summary>
    (byte[] Rgba, int Width, int Height) Render(int index, int width);
}

/// <summary>Renders only a region of a scaled page; zoom never allocates a full enlarged page.</summary>
public interface ITiledPdfDocument : IPdfDocument
{
    (byte[] Rgba, int Width, int Height) RenderRegion(int index, int pageWidth, PixelRect region);
}

/// <summary>
/// The pages of a PDF one below the other, drawn only near what is on screen (a long PDF does not take the memory of
/// all its pages); the document is closed when the view leaves the panel.
/// </summary>
public sealed class PdfPagesView : ScrollViewer, IDisposable
{
    public const int RenderWidth = 1000;
    private const int Keep = 4;
    private readonly IPdfDocument _doc;
    private readonly object _gate = new();
    private readonly Canvas _surface = new();
    private readonly Dictionary<int, Image> _pages = [];
    private readonly Dictionary<int, int> _resolutions = [];
    private sealed record Tile(int Page, int PageWidth, PixelRect Region);
    private readonly Dictionary<Tile, Image> _tiles = [];
    private readonly HashSet<Tile> _wantedTiles = [], _failedTiles = [];
    private readonly HashSet<int> _wanted = [], _failed = [];
    private readonly double[] _ratios, _starts;
    private volatile bool _closed;
    private bool _posted, _rendering, _singlePage;
    private readonly PdfWordSelection _textSelection=new(){ZIndex=200,IsVisible=false};
    private byte[]? _studyBytes;
    private string? _studyPath,_studyHash;
    private int _selectionVersion;
    private bool _selecting;
    public string? SelectionStatus {get;private set;}
    public event Action<ReaderQuote>? QuoteRequested;
    public event Action<ReaderQuote>? AskRequested;
    public void ConfigureStudy(string path,byte[] bytes,string hash){_studyPath=path;_studyBytes=bytes;_studyHash=hash;}
    public async Task EnableSelectionAsync(bool enabled=true)
    {
        if(enabled&&!_singlePage)SetSinglePage(true);
        _selecting=enabled;_textSelection.IsVisible=enabled;
        if(!_surface.Children.Contains(_textSelection))_surface.Children.Add(_textSelection);
        LayoutPages();if(enabled)await LoadSelectionWordsAsync();
    }
    private async Task LoadSelectionWordsAsync()
    {
        int version=++_selectionVersion,page=CurrentPage;
        _textSelection.SetWords([]);
        if(_studyBytes is not { } bytes){SelectionStatus="Camada textual indisponível para esta fonte. Use OCR da região.";return;}
        try
        {
            var words=await Task.Run(()=>Urbe.Core.Assistant.DocumentWordGeometry.Read(bytes,page));
            if(_closed||version!=_selectionVersion)return;
            _textSelection.SetWords(words);SelectionStatus=words.Count==0?"Sem palavras nesta página. Use OCR da região.":"Arraste sobre as palavras da página; use Seleção para copiar, citar, destacar ou comentar.";
        }
        catch(Exception ex)when(ex is not OutOfMemoryException){if(!_closed&&version==_selectionVersion)SelectionStatus="Não foi possível mapear texto desta página; use OCR da região.";}
    }
    public ReaderQuote? SelectedQuote(StudyAnnotationKind kind=StudyAnnotationKind.Citation) => _studyPath is { } path&&_studyHash is { } hash&&_textSelection.Selection is { } selection
        ?new ReaderQuote(path,CurrentPage,selection.Text,hash,kind,Region:selection.Region):null;
    public void CiteSelection(StudyAnnotationKind kind=StudyAnnotationKind.Citation,bool ask=false)
    {if(SelectedQuote(kind) is { } quote){if(ask)AskRequested?.Invoke(quote);else QuoteRequested?.Invoke(quote);}}

    private readonly Border _focusBox = new() { BorderBrush = Avalonia.Media.Brushes.DodgerBlue, BorderThickness = new Thickness(2), IsHitTestVisible = false, ZIndex = 100, IsVisible = false };
    private (int Page, Urbe.Core.ReaderRegion Region)? _focus;
    public Urbe.Core.ReaderRegion? FocusedRegion=>_focus?.Region;
    public void FocusRegion(int page, Urbe.Core.ReaderRegion region)
    {
        page=Math.Clamp(page,1,PageCount); _focus = (page,region);
        if (!_surface.Children.Contains(_focusBox)) _surface.Children.Add(_focusBox);
        GoToPage(page); LayoutPages();
        Offset = new Vector(_pageWidth * region.X, (_singlePage ? 0 : _starts[page-1]) + _pageWidth * _ratios[page-1] * region.Y);
    }
    private bool NeedsWork => _wanted.Any(i=>!_failed.Contains(i)&&(!_resolutions.TryGetValue(i,out int old)||old!=PixelWidth)) || _wantedTiles.Any(t=>!_tiles.ContainsKey(t)&&!_failedTiles.Contains(t));
    private double _pageWidth = 1;
    public double Zoom { get; private set; } = 1;
    public int PageCount { get; }
    public int CurrentPage { get; private set; } = 1;
    public event Action<int>? PageChanged;
    public int MaterializedTiles => _tiles.Count;
    public int MaterializedPages => _pages.Count;
    public IReadOnlyList<int> Drawn => _pages.Where(p => p.Value.Source is not null).Select(p => p.Key).Order().ToArray();
    protected override Type StyleKeyOverride => typeof(ScrollViewer);

    public PdfPagesView(IPdfDocument doc)
    {
        _doc = doc;
        PageCount = doc.PageCount;
        _ratios = Enumerable.Repeat(1.414, PageCount).ToArray();
        _starts = new double[PageCount + 1];
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
        Content = _surface;
        ScrollChanged += (_, _) => DrawSoon();
        SizeChanged += (_, _) => { LayoutPages(); DrawSoon(); };
        Loaded += (_, _) => { LayoutPages(); DrawSoon(); };
        DetachedFromVisualTree += (_, _) => Dispose();
    }

    public bool SinglePage => _singlePage;
    public void SetSinglePage(bool single)
    { if(!single){_selecting=false;_textSelection.IsVisible=false;_selectionVersion++;} _singlePage = single; LayoutPages(); GoToPage(CurrentPage); }
    public void SetZoom(double zoom)
    {
        if (_closed || !double.IsFinite(zoom) || zoom < .25 || zoom > 16) return;
        int page = CurrentPage;
        Zoom = zoom;
        _failed.Clear();
        LayoutPages();
        GoToPage(page);
    }

    private void LayoutPages()
    {
        _pageWidth = Math.Max(1, Viewport.Width > 1 ? Viewport.Width : Bounds.Width) * Zoom;
        for (int i = 0; i < PageCount; i++) _starts[i + 1] = _starts[i] + _pageWidth * _ratios[i] + 10;
        _surface.Width = _pageWidth;
        _surface.Height = Math.Max(1, _singlePage ? _pageWidth * _ratios[Math.Max(0, CurrentPage - 1)] : _starts[^1]);
        foreach (var (index, image) in _pages) Place(index, image);
        _textSelection.Width=_pageWidth;_textSelection.Height=_pageWidth*_ratios[Math.Max(0,CurrentPage-1)];Canvas.SetTop(_textSelection,_singlePage?0:_starts[Math.Max(0,CurrentPage-1)]);
        if (_focus is { } focus)
        {
            var r = focus.Region; double height = _pageWidth * _ratios[focus.Page-1];
            _focusBox.IsVisible = !_singlePage || focus.Page == CurrentPage;
            Canvas.SetLeft(_focusBox,r.X*_pageWidth);Canvas.SetTop(_focusBox,(_singlePage?0:_starts[focus.Page-1])+r.Y*height);
            _focusBox.Width=r.Width*_pageWidth;_focusBox.Height=r.Height*height;
        }
    }

    private void Place(int index, Image image)
    {
        image.IsVisible = !_singlePage || index == CurrentPage - 1;
        Canvas.SetTop(image, _singlePage ? 0 : _starts[index]);
        image.Width = _pageWidth;
        image.Height = _pageWidth * _ratios[index];
    }

    public void GoToPage(int page)
    {
        if (_closed || PageCount == 0) return;
        int index = Math.Clamp(page - 1, 0, PageCount - 1);
        if (_singlePage) { CurrentPage = index + 1; PageChanged?.Invoke(CurrentPage); LayoutPages(); if(_selecting)_ = LoadSelectionWordsAsync(); }
        Offset = new Vector(Offset.X, _singlePage ? 0 : _starts[index]);
        DrawSoon();
    }

    private int PageAt(double y)
    {
        int i = Array.BinarySearch(_starts, Math.Max(0, y));
        if (i < 0) i = ~i - 1;
        return Math.Clamp(i, 0, PageCount - 1);
    }

    private void DrawSoon()
    {
        if (_posted || _closed) return;
        _posted = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _posted = false;
            DrawVisible();
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void DrawVisible()
    {
        if (_closed || PageCount == 0) return;
        int first = _singlePage ? CurrentPage - 1 : PageAt(Offset.Y), last = _singlePage ? first : PageAt(Offset.Y + Math.Max(1, Viewport.Height));
        bool end = !_singlePage && Extent.Height > Viewport.Height + 1 && Offset.Y + Viewport.Height >= Extent.Height - 2;
        int page = end ? PageCount : first + 1;
        if (CurrentPage != page) { CurrentPage = page; PageChanged?.Invoke(page); }
        _wanted.Clear();
        for (int i = Math.Max(0, first - 1); i <= Math.Min(PageCount - 1, last + 1); i++)
        {
            _wanted.Add(i);
            if (_pages.ContainsKey(i)) continue;
            var image = new Image { Stretch = Stretch.Fill };
            _pages[i] = image;
            _surface.Children.Add(image);
            Place(i, image);
        }
        foreach (int i in _pages.Keys.Where(i => i < first - Keep || i > last + Keep).ToArray())
        {
            var image = _pages[i];
            _surface.Children.Remove(image);
            if (image.Source is IDisposable bitmap) { image.Source = null; bitmap.Dispose(); }
            _pages.Remove(i); _resolutions.Remove(i); _failed.Remove(i);
        }
        UpdateTiles(first, last);
        if (!_rendering && NeedsWork) _ = RenderAsync();
    }

    private double DisplayScale => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
    private int FullPixelWidth => Math.Max(RenderWidth, (int)Math.Ceiling(_pageWidth * DisplayScale));
    private int PixelWidth => Math.Min(1600, FullPixelWidth);
    private void UpdateTiles(int first, int last)
    {
        _wantedTiles.Clear();
        if (_doc is ITiledPdfDocument && FullPixelWidth > 1600)
        {
            int full = FullPixelWidth; double scale = full / _pageWidth;
            for (int page = first; page <= last; page++)
            {
                double top = _singlePage ? 0 : _starts[page];
                int height = (int)Math.Ceiling(full * _ratios[page]);
                int x1 = Math.Max(0, (int)(Offset.X * scale) / 1024 * 1024);
                int y1 = Math.Max(0, (int)((Offset.Y - top) * scale) / 1024 * 1024);
                int x2 = Math.Min(full, (int)Math.Ceiling((Offset.X + Viewport.Width) * scale));
                int y2 = Math.Min(height, (int)Math.Ceiling((Offset.Y + Viewport.Height - top) * scale));
                for (int y = y1; y < y2; y += 1024)
                    for (int x = x1; x < x2; x += 1024)
                        _wantedTiles.Add(new Tile(page,full,new PixelRect(x,y,Math.Min(1024,full-x),Math.Min(1024,height-y))));
            }
        }
        foreach (var key in _tiles.Keys.Where(t => !_wantedTiles.Contains(t)).ToArray())
        { var image = _tiles[key]; _surface.Children.Remove(image); (image.Source as IDisposable)?.Dispose(); _tiles.Remove(key); }
        _failedTiles.RemoveWhere(t=>!_wantedTiles.Contains(t));
    }

    private async Task RenderTilesAsync()
    {
        if (_doc is not ITiledPdfDocument tiled) return;
        while (!_closed)
        {
            var tile = _wantedTiles.FirstOrDefault(t => !_tiles.ContainsKey(t) && !_failedTiles.Contains(t));
            if (tile is null) return;
            try
            {
                var pixels = await Task.Run(() => { lock (_gate) { ObjectDisposedException.ThrowIf(_closed, this); return tiled.RenderRegion(tile.Page,tile.PageWidth,tile.Region); } });
                if (_closed) return;
                if (!_wantedTiles.Contains(tile)) continue;
                double scale = _pageWidth / tile.PageWidth;
                var image = new Image { Source = Pixels.ToBitmap(pixels.Rgba,pixels.Width,pixels.Height), Width = tile.Region.Width * scale, Height = tile.Region.Height * scale, Stretch = Stretch.Fill, IsHitTestVisible = false };
                Canvas.SetLeft(image,tile.Region.X * scale); Canvas.SetTop(image,(_singlePage ? 0 : _starts[tile.Page]) + tile.Region.Y * scale);
                _tiles[tile] = image; _surface.Children.Add(image);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { _failedTiles.Add(tile); }
        }
    }

    private async Task RenderAsync()
    {
        _rendering = true;
        try
        {
            while (!_closed)
            {
                int width = PixelWidth;
                int index = _wanted.OrderBy(i => Math.Abs(i - (CurrentPage - 1))).FirstOrDefault(i => !_failed.Contains(i) &&
                    (!_resolutions.TryGetValue(i, out int old) || old != width), -1);
                if (index < 0) break;
                try
                {
                    var rendered = await Task.Run(() =>
                    {
                        lock (_gate)
                        {
                            if (_closed) return ((byte[] Rgba, int Width, int Height)?)null;
                            return _doc.Render(index, width);
                        }
                    });
                    if (_closed) break;
                    if (rendered is not { } pixels || !_wanted.Contains(index) || width != PixelWidth) continue;
                    if (!_pages.TryGetValue(index, out var image)) continue;
                    double ratio = (double)pixels.Height / pixels.Width;
                    if (Math.Abs(_ratios[index] - ratio) > .001)
                    {
                        var within = Offset.Y - _starts[CurrentPage - 1];
                        _ratios[index] = ratio;
                        LayoutPages(); DrawSoon();
                        Offset = new Vector(Offset.X, _singlePage ? 0 : _starts[CurrentPage - 1] + within);
                    }
                    var old = image.Source as IDisposable;
                    image.Source = Pixels.ToBitmap(pixels.Rgba, pixels.Width, pixels.Height);
                    old?.Dispose();
                    _resolutions[index] = width;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                { _failed.Add(index); ToolTip.SetTip(this, "Não foi possível renderizar uma página deste PDF."); }
            }
        }
        finally { try { if (!_closed) await RenderTilesAsync(); } finally { _rendering = false; if (!_closed && NeedsWork) DrawSoon(); } }
    }

    public Task<byte[]> CapturePageAsync(int page, int width = 2000) => Task.Run(() =>
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            var pixels = _doc.Render(Math.Clamp(page - 1, 0, PageCount - 1), width);
            using var bitmap = Pixels.ToBitmap(pixels.Rgba, pixels.Width, pixels.Height);
            using var stream = new MemoryStream(); bitmap.Save(stream, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default); return stream.ToArray();
        }
    });

    public void Dispose()
    {
        if (_closed) return;
        _closed = true;
        foreach (var image in _pages.Values)
            if (image.Source is IDisposable bitmap) { image.Source = null; bitmap.Dispose(); }
        foreach (var image in _tiles.Values) (image.Source as IDisposable)?.Dispose();
        _tiles.Clear(); _wantedTiles.Clear();
        _pages.Clear(); _wanted.Clear(); _surface.Children.Clear();
        _ = Task.Run(() => { lock (_gate) _doc.Dispose(); });
    }
}

/// <summary>What the platform does for text input that Avalonia does not (the Android head sets it).</summary>
public static class PlatformInput
{
    /// <summary>Brings up the on-screen keyboard for the focused text box (null on a computer).</summary>
    public static Func<bool>? ShowKeyboard { get; set; }
}
