using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Urbe.Client;

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
    public static Func<string, string, Task>? Printer { get; set; }

    /// <summary>A viewer for <paramref name="request"/>: a PDF by the platform's reader, anything else by its factory.</summary>
    public static async Task<Control?> CreateAsync(ContentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Kind == Core.VaultFileKinds.Pdf && PdfReader is { } reader && await reader(request, cancellationToken) is { } pdf)
            return new PdfPagesView(pdf);
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

/// <summary>
/// The pages of a PDF one below the other, drawn only near what is on screen (a long PDF does not take the memory of
/// all its pages); the document is closed when the view leaves the panel.
/// </summary>
public sealed class PdfPagesView : ScrollViewer
{
    /// <summary>Pixels a page is drawn with.</summary>
    public const int RenderWidth = 1000;
    /// <summary>Pages kept drawn around the visible ones.</summary>
    private const int Keep = 4;

    private readonly IPdfDocument _doc;
    private readonly List<Image> _pages = [];
    private readonly StackPanel _stack = new() { Spacing = 10, Margin = new Thickness(0, 4) };
    private bool _closed;

    /// <summary>The theme's ScrollViewer template (a subclass gets none by itself, and would not scroll).</summary>
    protected override Type StyleKeyOverride => typeof(ScrollViewer);

    public PdfPagesView(IPdfDocument doc)
    {
        _doc = doc;
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        for (int i = 0; i < doc.PageCount; i++)
        {
            var size = doc.PageSize(i);
            var image = new Image { Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center };
            // the page's place is kept even before it is drawn, so scrolling does not jump
            var frame = new Border
            {
                Background = Brushes.White, Child = image, HorizontalAlignment = HorizontalAlignment.Stretch,
                Tag = size.Width > 0 ? size.Height / size.Width : 1.414
            };
            _pages.Add(image);
            _stack.Children.Add(frame);
        }
        Content = _stack;
        ScrollChanged += (_, _) => DrawSoon();
        SizeChanged += (_, e) =>
        {
            foreach (var frame in _stack.Children.OfType<Border>())
                frame.Height = Math.Max(1, e.NewSize.Width) * (double)frame.Tag!;
            DrawSoon();
        };
        Loaded += (_, _) => DrawSoon();
        DetachedFromVisualTree += (_, _) => Close();
    }

    /// <summary>The pages drawn now (tests).</summary>
    public IReadOnlyList<int> Drawn => _pages.Select((p, i) => (p, i)).Where(x => x.p.Source is not null).Select(x => x.i).ToList();

    public int PageCount => _pages.Count;

    private bool _drawPosted;

    /// <summary>Draws after the pages have their place (the layout runs before background work).</summary>
    private void DrawSoon()
    {
        if (_drawPosted) return;
        _drawPosted = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => { _drawPosted = false; DrawVisible(); }, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void DrawVisible()
    {
        if (_closed || _pages.Count == 0) return;
        double top = Offset.Y, bottom = top + Math.Max(1, Viewport.Height);
        int first = -1, last = -1;
        double y = 0;
        for (int i = 0; i < _stack.Children.Count; i++)
        {
            double h = _stack.Children[i].Bounds.Height + _stack.Spacing;
            if (y + h >= top && y <= bottom) { if (first < 0) first = i; last = i; }
            y += h;
        }
        if (first < 0) { first = 0; last = 0; }
        for (int i = 0; i < _pages.Count; i++)
        {
            bool near = i >= first - 1 && i <= last + 1;
            if (near && _pages[i].Source is null)
            {
                var (rgba, w, h) = _doc.Render(i, RenderWidth);
                _pages[i].Source = Pixels.ToBitmap(rgba, w, h);
            }
            else if (!near && (i < first - Keep || i > last + Keep) && _pages[i].Source is IDisposable old)
            {
                _pages[i].Source = null;
                old.Dispose();
            }
        }
    }

    private void Close()
    {
        if (_closed) return;
        _closed = true;
        foreach (var p in _pages) if (p.Source is IDisposable d) { p.Source = null; d.Dispose(); }
        _doc.Dispose();
    }
}

/// <summary>What the platform does for text input that Avalonia does not (the Android head sets it).</summary>
public static class PlatformInput
{
    /// <summary>Brings up the on-screen keyboard for the focused text box (null on a computer).</summary>
    public static Func<bool>? ShowKeyboard { get; set; }
}
