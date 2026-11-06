using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// The native comics reader (CST-238): one page of a <see cref="ComicBook"/> at a time, fitted to the screen or to its
/// width, with zoom (buttons, pinch, Ctrl+wheel). A tap on the left or right edge (or a swipe, or the arrows) turns the
/// page, a tap in the middle is the panel's (it hides the bars). Only the page shown and its neighbours are decoded;
/// the rest is released, so a 300-page book costs three pictures of memory.
/// </summary>
public sealed class ComicReaderView : DockPanel, IDisposable
{
    /// <summary>Pages kept decoded on each side of the one shown.</summary>
    public const int Neighbours = 1;
    /// <summary>The widest a page is decoded (it zooms from there) and the most pixels one page may take (a long webtoon strip is scaled down).</summary>
    public const int MaxPageWidth = 4096;
    public const long MaxPagePixels = 40L * 1000 * 1000;

    private readonly ComicBook _book;
    private readonly Dictionary<int, Task<DecodedPage>> _pages = [];
    private int _shownIndex = -1;
    private readonly Image _image = new() { Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly ScrollViewer _scroll;
    private readonly TextBlock _counter = new() { VerticalAlignment = VerticalAlignment.Center, FontSize = 13, MinWidth = 120, TextAlignment = TextAlignment.Center };
    private readonly TextBlock _problem = new() { TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 420, IsVisible = false };
    private readonly Slider _slider;
    private readonly Ui.UrbeIconButton _previous = new(UrbeTheme.Icons.Back, "Página anterior");
    private readonly Ui.UrbeIconButton _next = new(UrbeTheme.Icons.Forward, "Próxima página");
    private readonly Ui.UrbeIconButton _fit = new(UrbeTheme.Icons.Expand, "Ajustar à largura", 21);
    private readonly TextBlock _percent = new() { VerticalAlignment = VerticalAlignment.Center, FontSize = 12, MinWidth = 44, TextAlignment = TextAlignment.Center };
    private bool _syncingSlider, _disposed;
    private double _pinch = 1;
    private (Point At, long Ms)? _press;
    private int _version;

    public string Path { get; }
    public int PageCount => _book.PageCount;
    /// <summary>The page shown, from 0.</summary>
    public int CurrentPage { get; private set; }
    public double Zoom { get; private set; } = 1;
    /// <summary>The page fills the width (and scrolls down) instead of fitting the whole screen.</summary>
    public bool FitWidth { get; private set; }
    /// <summary>The pages decoded right now (tests: the reader keeps only the neighbourhood).</summary>
    public IReadOnlyCollection<int> DecodedPages => _pages.Keys;
    /// <summary>"Página 3 de 12".</summary>
    public string PageLabel => _counter.Text ?? "";
    /// <summary>Why the page shown could not be drawn, or null.</summary>
    public string? PageProblem => _problem.IsVisible ? _problem.Text : null;

    public event Action<int>? PageChanged;
    public event Action? CenterTapped;

    public ComicReaderView(string path, ComicBook book)
    {
        Path = path;
        _book = book;
        Focusable = true;
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        _counter.Foreground = UrbeTheme.Brush(UrbeTheme.Text2);
        _percent.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        _problem.Foreground = UrbeTheme.Brush(UrbeTheme.Text2);
        _scroll = new ScrollViewer
        {
            Content = _image,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        _slider = new Slider { Minimum = 1, Maximum = Math.Max(1, book.PageCount), Value = 1, IsSnapToTickEnabled = true, TickFrequency = 1, MinWidth = 120, VerticalAlignment = VerticalAlignment.Center, IsVisible = book.PageCount > 1 };
        Avalonia.Automation.AutomationProperties.SetName(_slider, "Ir para a página");
        _slider.PropertyChanged += (_, e) =>
        {
            if (e.Property != RangeBase.ValueProperty || _syncingSlider) return;
            int page = (int)Math.Round(_slider.Value) - 1;
            if (page != CurrentPage) _ = GoToPageAsync(page);
        };
        _previous.Click += (_, _) => _ = GoToPageAsync(CurrentPage - 1);
        _next.Click += (_, _) => _ = GoToPageAsync(CurrentPage + 1);
        _fit.Click += (_, _) => SetFitWidth(!FitWidth);
        var zoomOut = new Ui.UrbeIconButton(UrbeTheme.Icons.ZoomOut, "Diminuir", 21);
        zoomOut.Click += (_, _) => SetZoom(Zoom / 1.25);
        var zoomIn = new Ui.UrbeIconButton(UrbeTheme.Icons.ZoomIn, "Aumentar", 21);
        zoomIn.Click += (_, _) => SetZoom(Zoom * 1.25);
        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center,
            Children = { _previous, _counter, _next, _slider, zoomOut, _percent, zoomIn, _fit }
        };
        var bottom = new Border
        {
            Child = new ScrollViewer { Content = bar, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled },
            Padding = new Thickness(4, 2), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line), BorderThickness = new Thickness(0, 1, 0, 0), Background = UrbeTheme.Brush(UrbeTheme.Surface)
        };
        Bar = bottom;
        SetDock(bottom, Dock.Bottom);
        Children.Add(bottom);
        Children.Add(new Grid { Children = { _scroll, _problem } });
        _scroll.SizeChanged += (_, _) => Resize();
        _scroll.GestureRecognizers.Add(new PinchGestureRecognizer());
        _scroll.AddHandler(PinchEvent, (_, e) => { SetZoom(Zoom * e.Scale / _pinch); _pinch = e.Scale; e.Handled = true; });
        _scroll.AddHandler(PinchEndedEvent, (_, _) => _pinch = 1);
        _scroll.PointerWheelChanged += (_, e) => { if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return; SetZoom(Zoom * Math.Pow(1.15, e.Delta.Y)); e.Handled = true; };
        _scroll.AddHandler(PointerPressedEvent, (_, e) => _press = (e.GetPosition(_scroll), Environment.TickCount64), Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        _scroll.AddHandler(PointerReleasedEvent, (_, e) => { if (_press is { } from) { _press = null; Released(from, e.GetPosition(_scroll)); } },
            Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        KeyDown += (_, e) =>
        {
            int? target = e.Key switch
            {
                Key.Left or Key.PageUp => CurrentPage - 1,
                Key.Right or Key.PageDown or Key.Space => CurrentPage + 1,
                Key.Home => 0,
                Key.End => PageCount - 1,
                _ => null
            };
            if (e.Key is Key.OemPlus or Key.Add) { SetZoom(Zoom * 1.25); e.Handled = true; return; }
            if (e.Key is Key.OemMinus or Key.Subtract) { SetZoom(Zoom / 1.25); e.Handled = true; return; }
            if (target is not { } page) return;
            _ = GoToPageAsync(page);
            e.Handled = true;
        };
        DetachedFromVisualTree += (_, _) => _image.Source = null;
        AttachedToVisualTree += (_, _) => { if (!_disposed) _ = GoToPageAsync(CurrentPage); };
        UpdateBar();
    }

    /// <summary>The reader's own bar (page, zoom, fit); the panel hides it with its own bars.</summary>
    public Control Bar { get; }

    /// <summary>A tap: the left third turns back, the right third forward, the middle is the panel's; a swipe turns too.</summary>
    private void Released((Point At, long Ms) from, Point to)
    {
        var moved = to - from.At;
        double width = Math.Max(1, _scroll.Bounds.Width);
        bool panning = Zoom > 1 || FitWidth;
        if (!panning && Math.Abs(moved.X) > 60 && Math.Abs(moved.X) > Math.Abs(moved.Y) * 1.5)
        {
            _ = GoToPageAsync(CurrentPage + (moved.X < 0 ? 1 : -1));
            return;
        }
        if (Math.Abs(moved.X) > 8 || Math.Abs(moved.Y) > 8 || Environment.TickCount64 - from.Ms > 300) return;
        Tap(to.X / width);
    }

    /// <summary>A tap at a fraction of the width (0 left … 1 right).</summary>
    public void Tap(double fraction)
    {
        if (fraction < .3) _ = GoToPageAsync(CurrentPage - 1);
        else if (fraction > .7) _ = GoToPageAsync(CurrentPage + 1);
        else CenterTapped?.Invoke();
    }

    public async Task GoToPageAsync(int index)
    {
        if (_disposed || PageCount == 0) return;
        index = Math.Clamp(index, 0, PageCount - 1);
        bool changed = index != CurrentPage;
        CurrentPage = index;
        int version = ++_version;
        UpdateBar();
        Release();
        var current = Decode(index);
        for (int d = 1; d <= Neighbours; d++) { _ = Decode(index + d); _ = Decode(index - d); }
        if (changed) PageChanged?.Invoke(index);
        var page = await current;
        if (version != _version || _disposed) return;
        _image.Source = page.Bitmap;
        _shownIndex = page.Bitmap is null ? -1 : index;
        _problem.IsVisible = page.Bitmap is null;
        _problem.Text = page.Problem;
        _scroll.Offset = default;
        Resize();
    }

    private sealed record DecodedPage(Bitmap? Bitmap, string? Problem);

    private Task<DecodedPage> Decode(int index)
    {
        if (index < 0 || index >= PageCount) return Task.FromResult(new DecodedPage(null, null));
        if (_pages.TryGetValue(index, out var known)) return known;
        var book = _book;
        var task = Task.Run(() =>
        {
            try
            {
                return new DecodedPage(DecodePicture(book.PageBytes(index), MaxPageWidth, MaxPagePixels), null);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // a broken or disguised page is said on its own; the rest of the book still reads
                return new DecodedPage(null, "A página " + (index + 1) + " não pôde ser mostrada: " + ex.Message);
            }
        });
        _pages[index] = task;
        return task;
    }

    /// <summary>Every decoded page out of the neighbourhood is let go (its pixels freed now, or when it finishes decoding).</summary>
    private void Release()
    {
        foreach (var index in _pages.Keys.Where(i => Math.Abs(i - CurrentPage) > Neighbours).ToArray())
        {
            var task = _pages[index];
            _pages.Remove(index);
            if (index == _shownIndex) { _image.Source = null; _shownIndex = -1; }
            Free(task);
        }
    }

    /// <summary>
    /// A picture decoded at most <paramref name=maxWidth/> wide and <paramref name=maxPixels/> in all (read from its
    /// header): a huge scan or a lying header is scaled down, never allocated whole.
    /// </summary>
    public static Bitmap DecodePicture(byte[] bytes, int maxWidth, long maxPixels)
    {
        int? width = null;
        if (ImageHeader.Size(bytes) is var (w, h) && w > 0 && h > 0)
        {
            double target = Math.Min(w, maxWidth);
            if (target * (target * h / w) > maxPixels) target = Math.Min(target, Math.Sqrt(maxPixels * (double)w / h));
            if (target < w) width = Math.Max(1, (int)target);
        }
        using var input = new MemoryStream(bytes, writable: false);
        return width is { } scaled ? Bitmap.DecodeToWidth(input, scaled) : new Bitmap(input);
    }

    /// <summary>
    /// A page leaving the neighbours: its bitmap is released on the UI thread, after the frames that may still draw it,
    /// and never while it is the picture shown (a native bitmap freed under the renderer crashes the process).
    /// </summary>
    private void Free(Task<DecodedPage> task) =>
        _ = task.ContinueWith(t =>
        {
            if (!t.IsCompletedSuccessfully || t.Result.Bitmap is not { } bitmap) return;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (!ReferenceEquals(_image.Source, bitmap)) bitmap.Dispose(); }, Avalonia.Threading.DispatcherPriority.Background);
        }, TaskScheduler.Default);

    public void SetZoom(double zoom)
    {
        Zoom = Math.Clamp(zoom, 1, 8);
        Resize();
        UpdateBar();
    }

    public void SetFitWidth(bool fitWidth)
    {
        FitWidth = fitWidth;
        var tip = fitWidth ? "Ajustar à tela" : "Ajustar à largura";
        ToolTip.SetTip(_fit, tip);
        Avalonia.Automation.AutomationProperties.SetName(_fit, tip);
        _fit.Content = UrbeTheme.Icon(fitWidth ? UrbeTheme.Icons.Shrink : UrbeTheme.Icons.Expand, 21, UrbeTokens.Color.TextSecondary, 1.8);
        Resize();
    }

    private void Resize()
    {
        if (_image.Source is not Bitmap bitmap) return;
        var size = bitmap.Size;
        double viewW = Math.Max(1, _scroll.Bounds.Width), viewH = Math.Max(1, _scroll.Bounds.Height);
        double width = FitWidth ? viewW : Math.Min(viewW, viewH * size.Width / Math.Max(1, size.Height));
        _image.Width = width * Zoom;
        _image.Height = _image.Width * size.Height / Math.Max(1, size.Width);
    }

    private void UpdateBar()
    {
        _counter.Text = "Página " + (CurrentPage + 1) + " de " + PageCount;
        _previous.IsEnabled = CurrentPage > 0;
        _next.IsEnabled = CurrentPage < PageCount - 1;
        _percent.Text = Math.Round(Zoom * 100) + "%";
        _syncingSlider = true;
        _slider.Value = CurrentPage + 1;
        _syncingSlider = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _image.Source = null;
        foreach (var task in _pages.Values) Free(task);
        _pages.Clear();
        // the archive is closed once no page is being read from it
        _book.Dispose();
    }
}
