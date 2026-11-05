using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SkiaSharp;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// CST-237: the native reader of a PowerPoint deck. One slide at a time, drawn by <see cref="PptxSlideRenderer"/> at the
/// size it is shown (16:9, 4:3 or the deck's own proportion, never stretched), with thumbnails in a virtualized list
/// (only the visible ones are drawn, small, and a few kept), "Slide X de N", arrows, swipe and edge taps, zoom,
/// a full-screen presentation mode, search over slide text and speaker notes, and the notes of each slide. Only the
/// slide shown and its neighbours are kept drawn. What a slide has that is not drawn here is said above it, with
/// "Abrir em outro app" — a reading view, never a promise of PowerPoint's fidelity.
/// </summary>
public sealed class PptxReaderView : DockPanel, IDisposable
{
    public const int Neighbours = 1;
    /// <summary>The width thumbnails are drawn at, and how many are kept drawn.</summary>
    public const int ThumbnailWidth = 176;
    public const int ThumbnailCache = 48;

    private const string PlayIcon = "M8 5v14l11-7z";
    private const string GridIcon = "M4 4h7v7H4z M13 4h7v7h-7z M4 13h7v7H4z M13 13h7v7h-7z";

    private sealed record Rendered(Bitmap? Bitmap, IReadOnlyList<string> Problems, string? Error);

    private readonly PptxDeck _deck;
    private readonly Dictionary<int, (int Width, Task<Rendered> Task)> _slides = [];
    private readonly Dictionary<int, Bitmap> _thumbnails = [];
    private readonly LinkedList<int> _thumbnailOrder = new();
    private readonly HashSet<int> _thumbnailPending = [];
    private readonly SemaphoreSlim _thumbnailGate = new(1, 1);
    private readonly Image _image = new() { Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly ScrollViewer _scroll;
    private readonly Border _stage;
    private readonly TextBlock _counter = new() { VerticalAlignment = VerticalAlignment.Center, FontSize = 13, MinWidth = 110, TextAlignment = TextAlignment.Center };
    private readonly TextBlock _percent = new() { VerticalAlignment = VerticalAlignment.Center, FontSize = 12, MinWidth = 44, TextAlignment = TextAlignment.Center };
    private readonly TextBlock _notice = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _noticeRow;
    private readonly TextBlock _problem = new() { TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 420, IsVisible = false };
    private readonly SelectableTextBlock _notes = new() { FontSize = 14, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _notesHead = new() { FontSize = 11, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
    private readonly Border _notesPanel;
    private readonly ListBox _thumbs;
    private readonly Border _thumbsPanel;
    private readonly TextBox _query = new() { PlaceholderText = "Buscar nos slides e notas", MinHeight = 40, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly TextBlock _searchStatus = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) };
    private readonly ListBox _results = new() { MaxHeight = 180, IsVisible = false };
    private readonly Border _searchPanel;
    private readonly Ui.UrbeIconButton _previous = new(UrbeTheme.Icons.Back, "Slide anterior");
    private readonly Ui.UrbeIconButton _next = new(UrbeTheme.Icons.Forward, "Próximo slide");
    private readonly Ui.UrbeIconButton _exitPresenting = new(UrbeTheme.Icons.Close, "Sair da apresentação", 20) { IsVisible = false, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8), Opacity = .6 };
    private IReadOnlyList<PptxSearchHit> _hits = [];
    private int _hit = -1;
    private bool _syncingThumbs, _disposed, _notesWanted = true, _thumbsDecided;
    private (Point At, long Ms)? _press;
    private double _pinch = 1;
    private int _version;
    private int _shownIndex = -1;
    private Rendered? _shown;

    public string Path { get; }
    public int SlideCount => _deck.SlideCount;
    /// <summary>The slide shown, from 0.</summary>
    public int CurrentSlide { get; private set; }
    public double Zoom { get; private set; } = 1;
    public bool IsPresenting { get; private set; }
    public bool NotesVisible => _notesPanel.IsVisible;
    public bool ThumbnailsVisible => _thumbsPanel.IsVisible;
    /// <summary>"Slide 3 de 12".</summary>
    public string SlideLabel => _counter.Text ?? "";
    /// <summary>The speaker notes of the slide shown ("" when it has none).</summary>
    public string CurrentNotes => _deck.Slide(CurrentSlide).Notes;
    /// <summary>What of the slide shown is not drawn here, or null when nothing is left out.</summary>
    public string? Notice => _noticeRow.IsVisible ? _notice.Text : null;
    /// <summary>Why the slide shown could not be drawn, or null.</summary>
    public string? SlideProblem => _problem.IsVisible ? _problem.Text : null;
    /// <summary>The slides drawn at full size right now (tests: only the neighbourhood).</summary>
    public IReadOnlyCollection<int> RenderedSlides => _slides.Keys;
    /// <summary>The thumbnails drawn right now (tests: only what the list showed).</summary>
    public IReadOnlyCollection<int> RenderedThumbnails => _thumbnails.Keys;
    /// <summary>The pixel size of the slide shown (tests: the deck's proportion).</summary>
    public PixelSize? ShownPixels => (_image.Source as Bitmap)?.PixelSize;
    public IReadOnlyList<PptxSearchHit> SearchHits => _hits;
    /// <summary>Where the slide shown comes from: "aula.pptx · slide 3".</summary>
    public string Reference => System.IO.Path.GetFileName(Path) + " · slide " + (CurrentSlide + 1);
    public Control Bar { get; }

    public event Action<int>? SlideChanged;
    public event Action<bool>? PresentingChanged;
    public event Action? CenterTapped;
    public event Action? OpenExternallyRequested;

    public PptxReaderView(string path, PptxDeck deck)
    {
        Path = path;
        _deck = deck;
        Focusable = true;
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        _counter.Foreground = UrbeTheme.Brush(UrbeTheme.Text2);
        _percent.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        _problem.Foreground = UrbeTheme.Brush(UrbeTheme.Text2);
        _notice.Foreground = UrbeTheme.Brush(UrbeTheme.Text2);
        _notes.Foreground = UrbeTheme.Brush(UrbeTheme.Text);
        _notesHead.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        _searchStatus.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);

        // bottom bar: ‹ Slide 3 de 12 › · thumbnails · search · notes · − % + · present
        _previous.Click += (_, _) => _ = GoToSlideAsync(CurrentSlide - 1);
        _next.Click += (_, _) => _ = GoToSlideAsync(CurrentSlide + 1);
        var thumbs = new Ui.UrbeIconButton(GridIcon, "Miniaturas", 20);
        thumbs.Click += (_, _) => SetThumbnailsVisible(!ThumbnailsVisible);
        var search = new Ui.UrbeIconButton(UrbeTheme.Icons.Search, "Buscar nos slides", 20);
        search.Click += (_, _) => SetSearchVisible(!_searchPanel!.IsVisible);
        var notes = new Ui.UrbeIconButton(UrbeTheme.Icons.Notes, "Notas do apresentador", 20);
        notes.Click += (_, _) => SetNotesVisible(!_notesWanted);
        var zoomOut = new Ui.UrbeIconButton(UrbeTheme.Icons.ZoomOut, "Diminuir", 21);
        zoomOut.Click += (_, _) => SetZoom(Zoom / 1.25);
        var zoomIn = new Ui.UrbeIconButton(UrbeTheme.Icons.ZoomIn, "Aumentar", 21);
        zoomIn.Click += (_, _) => SetZoom(Zoom * 1.25);
        var present = new Ui.UrbeIconButton(PlayIcon, "Apresentar", 20);
        present.Click += (_, _) => SetPresenting(true);
        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center,
            Children = { _previous, _counter, _next, thumbs, search, notes, zoomOut, _percent, zoomIn, present }
        };
        Bar = new Border
        {
            Child = new ScrollViewer { Content = bar, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled },
            Padding = new Thickness(4, 2), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line), BorderThickness = new Thickness(0, 1, 0, 0), Background = UrbeTheme.Brush(UrbeTheme.Surface)
        };
        SetDock(Bar, Dock.Bottom);
        Children.Add(Bar);

        // speaker notes of the slide, selectable, under it
        _notesPanel = new Border
        {
            Child = new ScrollViewer { Content = new StackPanel { Children = { _notesHead, _notes } }, MaxHeight = 170 },
            Padding = new Thickness(12, 8), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line), BorderThickness = new Thickness(0, 1, 0, 0), Background = UrbeTheme.Brush(UrbeTheme.Surface)
        };
        SetDock(_notesPanel, Dock.Bottom);
        Children.Add(_notesPanel);

        // search over every slide's text and notes
        _query.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; await SearchOrNextAsync(_query.Text ?? ""); } };
        _results.SelectionChanged += (_, _) => { if (_results.SelectedIndex >= 0 && _results.SelectedIndex != _hit) _ = ShowHitAsync(_results.SelectedIndex); };
        var searchRow = new DockPanel();
        SetDock(_searchStatus, Dock.Right);
        searchRow.Children.Add(_searchStatus);
        searchRow.Children.Add(_query);
        _searchPanel = new Border
        {
            Child = new StackPanel { Spacing = 4, Children = { searchRow, _results } }, IsVisible = false,
            Padding = new Thickness(8, 6), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line), BorderThickness = new Thickness(0, 0, 0, 1), Background = UrbeTheme.Brush(UrbeTheme.Surface)
        };
        SetDock(_searchPanel, Dock.Top);
        Children.Add(_searchPanel);

        // what the slide has that is not drawn here, and the way out
        var open = new Ui.UrbeButton("Abrir em outro app", Ui.ButtonKind.Ghost) { VerticalAlignment = VerticalAlignment.Center };
        open.Click += (_, _) => OpenExternallyRequested?.Invoke();
        var noticeLine = new DockPanel();
        SetDock(open, Dock.Right);
        noticeLine.Children.Add(open);
        noticeLine.Children.Add(_notice);
        _noticeRow = new Border { Child = noticeLine, Padding = new Thickness(10, 4), Background = UrbeTheme.Brush(UrbeTheme.Surface2), IsVisible = false };
        SetDock(_noticeRow, Dock.Top);
        Children.Add(_noticeRow);

        // thumbnails: a virtualized list; each item draws its slide small when it is realized
        _thumbs = new ListBox
        {
            ItemsSource = Enumerable.Range(0, deck.SlideCount).ToArray(),
            ItemTemplate = new FuncDataTemplate<int>((_, _) => new Thumbnail(this), supportsRecycling: true),
            Background = Brushes.Transparent
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(_thumbs, ScrollBarVisibility.Disabled);
        _thumbs.SelectionChanged += (_, _) => { if (!_syncingThumbs && _thumbs.SelectedIndex >= 0 && _thumbs.SelectedIndex != CurrentSlide) _ = GoToSlideAsync(_thumbs.SelectedIndex); };
        _thumbsPanel = new Border
        {
            Child = _thumbs, Width = ThumbnailWidth + 28, IsVisible = false,
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line), BorderThickness = new Thickness(0, 0, 1, 0), Background = UrbeTheme.Brush(UrbeTheme.Surface)
        };
        SetDock(_thumbsPanel, Dock.Left);
        Children.Add(_thumbsPanel);

        _scroll = new ScrollViewer { Content = _image, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _stage = new Border { Child = new Grid { Children = { _scroll, _problem, _exitPresenting } }, Background = UrbeTheme.Brush(UrbeTheme.Bg) };
        Children.Add(_stage);
        _exitPresenting.Click += (_, _) => SetPresenting(false);

        _scroll.SizeChanged += (_, e) =>
        {
            Resize();
            if (e.PreviousSize.Width > 0 && Math.Abs(e.NewSize.Width - e.PreviousSize.Width) / e.PreviousSize.Width > .2) _ = GoToSlideAsync(CurrentSlide);
        };
        // thumbnails beside the slide on a wide screen; on a phone they wait for the button
        SizeChanged += (_, e) =>
        {
            if (_thumbsDecided || e.NewSize.Width < 100) return;
            _thumbsDecided = true;
            SetThumbnailsVisible(e.NewSize.Width >= 600 && deck.SlideCount > 1);
        };
        _scroll.GestureRecognizers.Add(new PinchGestureRecognizer());
        _scroll.AddHandler(PinchEvent, (_, e) => { SetZoom(Zoom * e.Scale / _pinch); _pinch = e.Scale; e.Handled = true; });
        _scroll.AddHandler(PinchEndedEvent, (_, _) => _pinch = 1);
        _scroll.PointerWheelChanged += (_, e) => { if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return; SetZoom(Zoom * Math.Pow(1.15, e.Delta.Y)); e.Handled = true; };
        _scroll.AddHandler(PointerPressedEvent, (_, e) => _press = (e.GetPosition(_scroll), Environment.TickCount64), Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        _scroll.AddHandler(PointerReleasedEvent, (_, e) => { if (_press is { } from) { _press = null; Released(from, e.GetPosition(_scroll)); } },
            Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        KeyDown += (_, e) =>
        {
            if (e.Source is TextBox) return;
            if (e.Key == Key.Escape && IsPresenting) { SetPresenting(false); e.Handled = true; return; }
            if (e.Key == Key.F5) { SetPresenting(true); e.Handled = true; return; }
            if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control)) { SetSearchVisible(true); e.Handled = true; return; }
            if (e.Key is Key.OemPlus or Key.Add) { SetZoom(Zoom * 1.25); e.Handled = true; return; }
            if (e.Key is Key.OemMinus or Key.Subtract) { SetZoom(Zoom / 1.25); e.Handled = true; return; }
            int? target = e.Key switch
            {
                Key.Left or Key.PageUp or Key.Up or Key.Back => CurrentSlide - 1,
                Key.Right or Key.PageDown or Key.Down or Key.Space or Key.Enter => CurrentSlide + 1,
                Key.Home => 0,
                Key.End => SlideCount - 1,
                _ => null
            };
            if (target is not { } slide) return;
            _ = GoToSlideAsync(slide);
            e.Handled = true;
        };
        DetachedFromVisualTree += (_, _) => _image.Source = null;
        AttachedToVisualTree += (_, _) => { if (!_disposed) _ = GoToSlideAsync(CurrentSlide); };
        UpdateBar();
    }

    private void Released((Point At, long Ms) from, Point to)
    {
        var moved = to - from.At;
        double width = Math.Max(1, _scroll.Bounds.Width);
        if (Zoom <= 1 && Math.Abs(moved.X) > 60 && Math.Abs(moved.X) > Math.Abs(moved.Y) * 1.5)
        {
            _ = GoToSlideAsync(CurrentSlide + (moved.X < 0 ? 1 : -1));
            return;
        }
        if (Math.Abs(moved.X) > 8 || Math.Abs(moved.Y) > 8 || Environment.TickCount64 - from.Ms > 300) return;
        Tap(to.X / width);
    }

    /// <summary>A tap at a fraction of the width: the left third back, the right third forward, the middle is the panel's; presenting, anything past the left third goes forward.</summary>
    public void Tap(double fraction)
    {
        if (fraction < .3) _ = GoToSlideAsync(CurrentSlide - 1);
        else if (fraction > .7 || IsPresenting) _ = GoToSlideAsync(CurrentSlide + 1);
        else CenterTapped?.Invoke();
    }

    public async Task GoToSlideAsync(int index)
    {
        if (_disposed || SlideCount == 0) return;
        index = Math.Clamp(index, 0, SlideCount - 1);
        bool changed = index != CurrentSlide;
        CurrentSlide = index;
        int version = ++_version;
        UpdateBar();
        UpdateSlideInfo(null);
        int width = WantedWidth();
        Release();
        var current = Render(index, width);
        for (int d = 1; d <= Neighbours; d++) { _ = Render(index + d, width); _ = Render(index - d, width); }
        if (changed) SlideChanged?.Invoke(index);
        var slide = await current;
        if (version != _version || _disposed) return;
        _image.Source = slide.Bitmap;
        _shownIndex = slide.Bitmap is null ? -1 : index;
        _shown = slide;
        _problem.IsVisible = slide.Bitmap is null;
        _problem.Text = slide.Error;
        UpdateSlideInfo(slide);
        if (changed) _scroll.Offset = default;
        Resize();
    }

    /// <summary>The pixel width a slide is drawn at: as wide as it is shown, times the screen's density and the zoom.</summary>
    private int WantedWidth()
    {
        double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        double viewW = Math.Max(320, _scroll.Bounds.Width), viewH = Math.Max(180, _scroll.Bounds.Height);
        double shown = Math.Min(viewW, viewH * _deck.Width / _deck.Height) * Math.Min(Zoom, 3);
        return (int)Math.Clamp(Math.Ceiling(shown * scaling / 64) * 64, 320, PptxSlideRenderer.MaxWidth);
    }

    private Task<Rendered> Render(int index, int width)
    {
        if (index < 0 || index >= SlideCount) return Task.FromResult(new Rendered(null, [], null));
        if (_slides.TryGetValue(index, out var known) && known.Width >= width) return known.Task;
        if (_slides.Remove(index, out var smaller)) Free(smaller.Task, index);
        var deck = _deck;
        var task = Task.Run(() =>
        {
            var problems = new List<string>();
            try
            {
                using var skia = PptxSlideRenderer.Render(deck, index, width, problems);
                return new Rendered(PptxSlideRenderer.ToAvalonia(skia), problems.Distinct().ToArray(), null);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                return new Rendered(null, problems, "O slide " + (index + 1) + " não pôde ser desenhado: " + ex.Message);
            }
        });
        _slides[index] = (width, task);
        return task;
    }

    private void Release()
    {
        foreach (var index in _slides.Keys.Where(i => Math.Abs(i - CurrentSlide) > Neighbours).ToArray())
        {
            var (_, task) = _slides[index];
            _slides.Remove(index);
            Free(task, index);
        }
    }

    private void Free(Task<Rendered> task, int index)
    {
        if (index == _shownIndex && task.IsCompletedSuccessfully && ReferenceEquals(task.Result, _shown)) return;
        _ = task.ContinueWith(t => { if (t.IsCompletedSuccessfully) Dispatcher.UIThread.Post(() => { if (!ReferenceEquals(_image.Source, t.Result.Bitmap)) t.Result.Bitmap?.Dispose(); }); }, TaskScheduler.Default);
    }

    private void UpdateSlideInfo(Rendered? rendered)
    {
        var slide = _deck.Slide(CurrentSlide);
        var parts = new List<string>();
        if (slide.Hidden) parts.Add("Slide oculto na apresentação original.");
        var missing = slide.NotRendered.Concat(rendered?.Problems ?? []).Distinct().ToArray();
        if (missing.Length > 0) parts.Add("Não mostrado neste slide: " + string.Join(", ", missing) + ".");
        if (_deck.DeckNotices.Count > 0) parts.Add("Na apresentação: " + string.Join(", ", _deck.DeckNotices) + ".");
        _notice.Text = string.Join(" ", parts);
        _noticeRow.IsVisible = parts.Count > 0 && !IsPresenting;
        _notes.Text = slide.Notes.Length > 0 ? slide.Notes : "Este slide não tem notas do apresentador.";
        _notesHead.Text = "NOTAS DO APRESENTADOR · " + Reference;
        _notesPanel.IsVisible = _notesWanted && !IsPresenting;
    }

    public void SetNotesVisible(bool visible)
    {
        _notesWanted = visible;
        _notesPanel.IsVisible = visible && !IsPresenting;
    }

    public void SetThumbnailsVisible(bool visible)
    {
        _thumbsDecided = true;
        _thumbsPanel.IsVisible = visible && !IsPresenting;
        if (visible) SyncThumbs();
    }

    public void SetSearchVisible(bool visible)
    {
        _searchPanel.IsVisible = visible && !IsPresenting;
        if (visible) _query.Focus();
        else Focus();
    }

    /// <summary>Full screen, black around the slide, no bars; Esc, Back or ✕ leave it.</summary>
    public void SetPresenting(bool presenting)
    {
        if (IsPresenting == presenting) return;
        IsPresenting = presenting;
        if (presenting) _thumbsWasVisible = _thumbsPanel.IsVisible;
        Bar.IsVisible = !presenting;
        _thumbsPanel.IsVisible = !presenting && _thumbsWasVisible;
        _searchPanel.IsVisible = false;
        _exitPresenting.IsVisible = presenting;
        _stage.Background = presenting ? Brushes.Black : UrbeTheme.Brush(UrbeTheme.Bg);
        if (presenting) SetZoom(1);
        UpdateSlideInfo(_shown);
        PresentingChanged?.Invoke(presenting);
        Focus();
    }

    private bool _thumbsWasVisible;

    public void SetZoom(double zoom)
    {
        double before = Zoom;
        Zoom = Math.Clamp(zoom, 1, 6);
        Resize();
        UpdateBar();
        // drawn again, sharper, once the zoom asks for more pixels than the slide has
        if (Zoom > before && _image.Source is Bitmap b && WantedWidth() > b.PixelSize.Width * 1.2) _ = GoToSlideAsync(CurrentSlide);
    }

    private void Resize()
    {
        double viewW = Math.Max(1, _scroll.Bounds.Width), viewH = Math.Max(1, _scroll.Bounds.Height);
        double ratio = _deck.Width / _deck.Height;
        double width = Math.Min(viewW, viewH * ratio);
        _image.Width = width * Zoom;
        _image.Height = _image.Width / ratio;
    }

    private void UpdateBar()
    {
        _counter.Text = "Slide " + (CurrentSlide + 1) + " de " + SlideCount;
        _previous.IsEnabled = CurrentSlide > 0;
        _next.IsEnabled = CurrentSlide < SlideCount - 1;
        _percent.Text = Math.Round(Zoom * 100) + "%";
        SyncThumbs();
    }

    private void SyncThumbs()
    {
        _syncingThumbs = true;
        _thumbs.SelectedIndex = CurrentSlide;
        _syncingThumbs = false;
        if (_thumbsPanel.IsVisible) _thumbs.ScrollIntoView(CurrentSlide);
    }

    // ---------------------------------------------------------------- search

    /// <summary>Searches the deck (a new query) or goes to the next match (the same query again).</summary>
    public async Task SearchOrNextAsync(string query)
    {
        query = query.Trim();
        if (query.Length == 0) { _hits = []; _results.IsVisible = false; _searchStatus.Text = ""; return; }
        if (query == _lastQuery && _hits.Count > 0) { await ShowHitAsync((_hit + 1) % _hits.Count); return; }
        _lastQuery = query;
        _searchStatus.Text = "Buscando…";
        var deck = _deck;
        IReadOnlyList<PptxSearchHit> hits;
        try { hits = await Task.Run(() => deck.Search(query)); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { _searchStatus.Text = "A busca falhou: " + ex.Message; return; }
        if (_disposed || query != _lastQuery) return;
        _hits = hits;
        _hit = -1;
        int slides = hits.Select(h => h.Slide).Distinct().Count();
        _searchStatus.Text = hits.Count == 0 ? "Nenhum resultado" : hits.Count + (hits.Count == 1 ? " resultado" : " resultados") + " em " + slides + (slides == 1 ? " slide" : " slides");
        _results.ItemsSource = hits.Select(h => "Slide " + (h.Slide + 1) + (h.InNotes ? " · notas" : "") + " — " + h.Excerpt).ToArray();
        _results.IsVisible = hits.Count > 0;
        if (hits.Count > 0) await ShowHitAsync(0);
    }

    private string? _lastQuery;

    private async Task ShowHitAsync(int index)
    {
        if (index < 0 || index >= _hits.Count) return;
        _hit = index;
        _results.SelectedIndex = index;
        if (_hits[index].InNotes) SetNotesVisible(true);
        await GoToSlideAsync(_hits[index].Slide);
    }

    // ---------------------------------------------------------------- thumbnails

    private void RequestThumbnail(int index, Image target)
    {
        if (_thumbnails.TryGetValue(index, out var known)) { target.Source = known; Touch(index); return; }
        target.Source = null;
        if (!_thumbnailPending.Add(index)) return;
        var deck = _deck;
        _ = Task.Run(async () =>
        {
            await _thumbnailGate.WaitAsync();
            try
            {
                if (_disposed) return null;
                using var skia = PptxSlideRenderer.Render(deck, index, ThumbnailWidth);
                return PptxSlideRenderer.ToAvalonia(skia);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
            finally { _thumbnailGate.Release(); }
        }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            _thumbnailPending.Remove(index);
            if (!t.IsCompletedSuccessfully || t.Result is not { } bitmap) return;
            if (_disposed) { bitmap.Dispose(); return; }
            _thumbnails[index] = bitmap;
            Touch(index);
            while (_thumbnailOrder.Count > ThumbnailCache && _thumbnailOrder.Last is { } oldest)
            {
                _thumbnailOrder.RemoveLast();
                if (_thumbnails.Remove(oldest.Value, out var gone))
                {
                    foreach (var shown in _thumbs.GetRealizedContainers().SelectMany(c => c.GetVisualDescendantsOfType<Image>()).Where(i => ReferenceEquals(i.Source, gone))) shown.Source = null;
                    gone.Dispose();
                }
            }
            foreach (var item in _thumbs.GetRealizedContainers().SelectMany(c => c.GetVisualDescendantsOfType<Thumbnail>()).Where(x => x.Index == index)) item.Show(bitmap);
        }), TaskScheduler.Default);
    }

    private void Touch(int index)
    {
        _thumbnailOrder.Remove(index);
        _thumbnailOrder.AddFirst(index);
    }

    /// <summary>One item of the thumbnails list: its number and its slide, drawn when the list realizes it.</summary>
    private sealed class Thumbnail : StackPanel
    {
        private readonly PptxReaderView _owner;
        private readonly Image _picture = new() { Stretch = Stretch.Uniform };
        private readonly TextBlock _number = new() { FontSize = 11, VerticalAlignment = VerticalAlignment.Top, Width = 18, TextAlignment = TextAlignment.Right };
        public int Index { get; private set; } = -1;

        public Thumbnail(PptxReaderView owner)
        {
            _owner = owner;
            Orientation = Orientation.Horizontal;
            Spacing = 6;
            _number.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
            _picture.Width = ThumbnailWidth - 24;
            _picture.Height = _picture.Width * owner._deck.Height / owner._deck.Width;
            Children.Add(_number);
            Children.Add(new Border { Child = _picture, BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1), Background = Brushes.White });
            DataContextChanged += (_, _) =>
            {
                if (DataContext is not int index || index == Index) return;
                Index = index;
                _number.Text = (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                Avalonia.Automation.AutomationProperties.SetName(this, "Slide " + (index + 1));
                _owner.RequestThumbnail(index, _picture);
            };
        }

        public void Show(Bitmap bitmap) => _picture.Source = bitmap;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _image.Source = null;
        foreach (var (_, task) in _slides.Values) _ = task.ContinueWith(t => { if (t.IsCompletedSuccessfully) t.Result.Bitmap?.Dispose(); }, TaskScheduler.Default);
        _slides.Clear();
        foreach (var image in _thumbs.GetRealizedContainers().SelectMany(c => c.GetVisualDescendantsOfType<Image>())) image.Source = null;
        foreach (var bitmap in _thumbnails.Values) bitmap.Dispose();
        _thumbnails.Clear();
        _thumbnailGate.Wait();
        try { _deck.Dispose(); }
        finally { _thumbnailGate.Release(); }
    }
}

internal static class VisualSearch
{
    public static IEnumerable<T> GetVisualDescendantsOfType<T>(this Control root) where T : Control =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root).OfType<T>();
}
