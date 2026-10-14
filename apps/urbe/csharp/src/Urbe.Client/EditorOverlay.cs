using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Urbe.Core;

namespace Urbe.Client;

public enum EditorMode { Visual, Fonte }

/// <summary>
/// The 1.8.4 full editor (#editorFull) over the city: top bar with back, file name and Visual/Fonte, the Markdown
/// toolbar of the Fonte mode (LegacyEditorTools, checked against the original by tools/editor-oracle.mjs), the code tools
/// of non-Markdown files, wikilink suggestions after "[[", the centred text column, the status line
/// ("MD · n linhas · m palavras") and autosave through the vault host. Visual mode is <see cref="VisualSurface"/>.
/// Text is canonical in <see cref="Editor"/> (what Fonte shows and what is saved); Visual reads it on entering and
/// writes it back on every edit, like bodyEditor in app.js.
/// </summary>
public sealed class EditorOverlay : Grid
{
    private readonly TextBlock _title = new();
    private readonly TextBlock _status = new();
    private readonly TextBlock _saveState = new();
    private readonly VisualSurface _visual = new();
    private readonly WikiSuggestList _wiki = new();
    private readonly ScrollViewer _visualScroll;
    private readonly ScrollViewer _sourceScroll;
    private readonly StackPanel _sourceColumn;
    private readonly Border _properties;
    private readonly TextBlock _propertiesText = new();

    /// <summary>propertiesFor(b) of the open note, as renderProps writes it ("---\ntítulo: …\n---"), or null when the
    /// file has no house; the host supplies it.</summary>
    public Func<string, string?>? PropertiesFor { get; set; }
    private readonly Border _toolbarHost = new();
    private readonly StackPanel _markdownTools = new() { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(8, 0) };
    private readonly StackPanel _codeTools = new() { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(10, 0) };
    private Button _visualButton = null!, _sourceButton = null!;
    private Border _segment = null!;
    private CityNote? _note;
    private string _savedContent = string.Empty;
    private bool _saving, _fromVisual, _opening;
    private readonly DispatcherTimer _autosave = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private KnowledgeIndex? _index;
    private DocumentStore? _store;
    private string? _currentId;

    public bool SaveOnClose { get; set; }
    public bool HasChanges => _note is not null && !string.Equals(Editor.Text, _savedContent, StringComparison.Ordinal);
    public event Func<CityNote, Task>? SaveRequested;

    /// <summary>The split pane (editor.split): another note, read only, beside this one.</summary>
    public SplitPane Split { get; } = new();

    /// <summary>state.ratio: the editor's share of the width (.25–.75; the pane gets the rest).</summary>
    public double SplitRatio { get; set; } = .5;
    public EditorMode Mode { get; private set; } = EditorMode.Visual;
    /// <summary>"Abrir notas no modo" (Personalização → editor.modoInicial): Visual unless "texto".</summary>
    public bool StartInVisual { get; set; } = true;
    /// <summary>The notes of the open vault (for [[ suggestions and wikilink targets).</summary>
    public Func<IEnumerable<CityNote>>? Notes { get; set; }
    /// <summary>UD.prompt / UD.choose of the host.</summary>
    public UrbeDialogs? Dialogs { get; set; }
    public VisualSurface Surface => _visual;

    /// <summary>Draws the note again in Visual (the colours of the tags changed in Personalização).</summary>
    public void RefreshVisual()
    {
        if (_note is not null && Mode == EditorMode.Visual) _visual.SetText(Editor.Text ?? "");
        if (Pinned.Text is { } pinned && Pinned.SourcePath is { } from) Pinned.Pin(pinned, from);
    }

    /// <summary>"Fixar em painel" (DEC-0045, UC-39): the reference kept at the top of the editor, across notes.</summary>
    public PinnedPanel Pinned { get; } = new();

    /// <summary>Pins the selected text (of the block in Visual, of the text in Fonte), else the whole note.</summary>
    public void PinSelectionOrNote()
    {
        if (_note is null) return;
        string? text;
        if (Mode == EditorMode.Visual) text = _visual.SelectedText;
        else
        {
            var all = Editor.Text ?? "";
            int a = Math.Clamp(Math.Min(Editor.SelectionStart, Editor.SelectionEnd), 0, all.Length);
            int b = Math.Clamp(Math.Max(Editor.SelectionStart, Editor.SelectionEnd), 0, all.Length);
            text = b > a ? all[a..b] : null;
        }
        if (string.IsNullOrWhiteSpace(text)) text = Editor.Text ?? "";
        Pinned.Pin(text, _note.Path);
    }

    /// <summary>Personalização → Editor and Texto: the text column width, the Fonte font size and the line height factor (--ui-editor-width, --ui-code-size, --ui-editor-line).</summary>
    public void ApplyAppearance(double columnWidth, double codeSize, double lineHeight)
    {
        Editor.MaxWidth = columnWidth;
        Editor.FontSize = codeSize;
        Editor.LineHeight = codeSize * lineHeight;
    }

    public EditorOverlay()
    {
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        // top bar, find, toolbar (wide), text, footer, toolbar (narrow: on the last row, right above the soft keyboard)
        RowDefinitions = new RowDefinitions("56,Auto,Auto,*,48,Auto");
        Editor = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = false,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = UrbeTheme.MonoFont,
            FontSize = 15,
            LineHeight = 15 * 1.7,
            Foreground = UrbeTheme.Brush(UrbeTheme.Text),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CaretBrush = UrbeTheme.Brush(UrbeTheme.Accent),
            SelectionBrush = UrbeTheme.Brush(UrbeTheme.Accent, .35),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            MinHeight = 320
        };
        // 1.8.4: the text column has no box of its own; keep Fluent from adding one on hover/focus.
        foreach (var key in new[] { "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused" })
            Editor.Resources[key] = Brushes.Transparent;
        foreach (var key in new[] { "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused" })
            Editor.Resources[key] = Brushes.Transparent;
        Editor.TextChanged += (_, _) => OnTextChanged();
        Editor.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.CaretIndexProperty) UpdateWiki();
            if (e.Property == TextBox.CaretIndexProperty || e.Property == TextBox.SelectionStartProperty || e.Property == TextBox.SelectionEndProperty)
                UpdateActiveTools();
        };
        Editor.AddHandler(KeyDownEvent, (object? _, KeyEventArgs e) => { if (_wiki.HandleKey(e.Key)) e.Handled = true; }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        // math/editor.js: Ctrl+M inserts a formula, Ctrl+Shift+M a display formula, with the focus in the text
        AddHandler(KeyDownEvent, (object? _, KeyEventArgs e) =>
        {
            var mod = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
            if (!mod || e.KeyModifiers.HasFlag(KeyModifiers.Alt) || e.Key != Key.M || _note is null) return;
            e.Handled = true;
            _ = StartFormulaAsync(e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _wiki.Chosen += CommitWiki;

        _visual.TextEdited += md =>
        {
            _fromVisual = true;
            if (!string.Equals(Editor.Text, md, StringComparison.Ordinal)) Editor.Text = md;
            _fromVisual = false;
        };
        _visual.WikiLinkTapped += name => _ = FollowLinkAsync(name);
        _visual.PinRequested += text => { if (_note is not null) Pinned.Pin(text, _note.Path); };
        _visual.ActiveToolsChanged += on => { if (Mode == EditorMode.Visual) ShowActiveTools(on); };
        _visual.WikiCandidates = Candidates;
        _visual.AskUrl = AskUrlAsync;

        AddRow(TopBar(), 0);
        // find-ui.js: the bar is prepended to #editorMain, above #mdToolbar
        FindBar = new EditorFindBar(() => Editor, () => SetMode(EditorMode.Fonte));
        AddRow(FindBar, 1);
        AddRow(Toolbars(), ToolbarRowTop);
        SizeChanged += (_, e) => PlaceToolbar(e.NewSize.Width);
        // #propertiesBox (base.css + theme.css): above the text in Fonte only; max 720px, 12px below
        _properties = new Border
        {
            BorderBrush = UrbeTheme.Brush("#9c7b45"),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Background = UrbeTheme.Brush("#111b22"),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(10, 9),
            Margin = new Thickness(0, 0, 0, 12),
            IsVisible = false,
            Child = _propertiesText
        };
        _propertiesText.Foreground = UrbeTheme.Brush("#b8c6ce");
        _propertiesText.FontSize = 11;
        _propertiesText.LineHeight = 16.5;
        _propertiesText.TextWrapping = TextWrapping.Wrap;
        // the text column fills the width up to the column size (the textarea is width:100%)
        _sourceColumn = new StackPanel { Children = { _properties, Editor, _wiki }, HorizontalAlignment = HorizontalAlignment.Stretch };
        _sourceScroll = Column(_sourceColumn);
        _visualScroll = Column(_visual);
        // #mathDock: the formula bar of Fonte, over the bottom of the text
        MathBar = new MathDock(Editor, () => Mode == EditorMode.Fonte && IsVisible);
        var text = new Grid { Children = { _sourceScroll, _visualScroll, MathBar, Context } };
        text.SizeChanged += (_, e) => { MathBar.Place(e.NewSize.Width); Context.Place(e.NewSize.Width); };
        // #urbeSplitPane: another note beside this one, (1 - ratio) of the width (editor/split-ui.js)
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        body.Children.Add(text);
        SetColumn(Split, 1);
        body.Children.Add(Split);
        body.SizeChanged += (_, e) => Split.Width = Math.Round(e.NewSize.Width * (1 - SplitRatio));
        Context.LineChosen += GoToLine;
        // the pinned reference above the text, up to 40 % of the height, with its own scroll (DEC-0045, UC-39)
        var bodyHost = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*") };
        bodyHost.Children.Add(_tabBar);
        SetRow(Pinned, 1);
        bodyHost.Children.Add(Pinned);
        SetRow(body, 2);
        bodyHost.Children.Add(body);
        bodyHost.SizeChanged += (_, e) => Pinned.MaxHeight = Math.Max(120, e.NewSize.Height * .4);
        AddRow(bodyHost, 3);
        AddRow(Footer(), 4);
        _autosave.Tick += async (_, _) => await AutosaveTickAsync();
        ApplyOptions(null, null);
        ShowMode(EditorMode.Visual);
    }

    public TextBox Editor { get; }
    public event EventHandler? Closed;

    /// <summary>A note was opened in the editor (by any way: city, Notas, a link, a tab).</summary>
    public event Action<string>? Shown;

    /// <summary>The formula pane (math/editor.js openPane) the host shows: the LaTeX, whether it is a display formula and
    /// whether an existing one is edited in, the answer out.</summary>
    public Func<string, bool, bool, Task<FormulaPane.Result?>>? AskFormula
    {
        get => _askFormula;
        set { _askFormula = value; _visual.AskFormula = value; }
    }
    private Func<string, bool, bool, Task<FormulaPane.Result?>>? _askFormula;

    /// <summary>
    /// startFormula (∑ in the bar, Ctrl+M; Ctrl+Shift+M for a display formula): in the Visual mode the pane opens; in Fonte
    /// the snippet goes in at the selection, $…$ or, on its own lines, $$ … $$, with the caret where the formula goes.
    /// </summary>
    public async Task StartFormulaAsync(bool block)
    {
        if (_note is null) return;
        if (Mode == EditorMode.Visual)
        {
            if (AskFormula is { } ask) await _visual.FormulaAsync((t, b) => ask(t, b, false), block);
            return;
        }
        var text = Editor.Text ?? "";
        int a = Math.Min(Editor.SelectionStart, Editor.SelectionEnd), b = Math.Max(Editor.SelectionStart, Editor.SelectionEnd);
        var txt = text[a..b];
        var pre = block && a > 0 && text[a - 1] != '\n' ? "\n" : "";
        var sn = MathEditing.Snippet(block ? pre + "$$\n" + (txt.Length > 0 ? txt : "●") + "\n$$\n" : "$" + (txt.Length > 0 ? txt : "●") + "$");
        ApplyEdit(new LegacyEditorTools.TextEdit(text[..a] + sn.Text + text[b..], a + sn.Caret, a + sn.Caret));
    }

    /// <summary>The formula bar of Fonte (math/editor.js #mathDock).</summary>
    public MathDock MathBar { get; }

    /// <summary>"Estrutura e backlinks" (editor/chrome.js .uec-context).</summary>
    public EditorContextPanel Context { get; } = new();

    /// <summary>The outline's tap: the caret at the start of that line of the text, focused.</summary>
    private void GoToLine(int line)
    {
        var text = Editor.Text ?? "";
        int pos = 0;
        for (int i = 1; i < line && pos <= text.Length; i++)
        {
            int nl = text.IndexOf('\n', pos);
            if (nl < 0) { pos = text.Length; break; }
            pos = nl + 1;
        }
        pos = Math.Min(pos, text.Length);
        Editor.SelectionStart = Editor.SelectionEnd = Editor.CaretIndex = pos;
        if (Mode == EditorMode.Fonte) Editor.Focus();
    }

    /// <summary>"Localizar e substituir" (ui.find.open, Ctrl+F).</summary>
    public EditorFindBar FindBar { get; }

    // ------------------------------------------------------------------ open / close

    /// <summary>Opens a note fresh (from the city, Notas, a search…): the trail of followed links starts over.</summary>
    public void Open(CityNote note)
    {
        _trail.Clear();
        Show(note);
    }

    /// <summary>
    /// Notes left by following a [[link]] or a backlink, most recent on top. The 1.8.4 always closed the editor on
    /// Voltar; here Voltar walks this trail back first and only closes when it is empty (DEC-0045, UC-37).
    /// </summary>
    private readonly Stack<string> _trail = new();

    /// <summary>How many notes Voltar goes back through before closing.</summary>
    public int TrailDepth => _trail.Count;

    /// <summary>Creates the note a [[link]] names when it does not exist yet (DEC-0045, UC-37): the host writes it to
    /// the vault and returns it without opening it, or null when it could not.</summary>
    public Func<string, Task<CityNote?>>? CreateLinked { get; set; }

    /// <summary>Follows a link or a backlink to <paramref name="target"/>: saves the open note, remembers it and opens the target.</summary>
    public async Task NavigateAsync(CityNote target)
    {
        if (!await LeaveCurrentAsync()) return;
        Go(target);
    }

    private void Go(CityNote target)
    {
        if (_note is { } current && current.Path != target.Path) _trail.Push(current.Path);
        Show(target.Path == _note?.Path ? Current! : target);
    }

    /// <summary>Voltar: the previous note of the trail, or closes back to where the editor was opened from.</summary>
    public async Task BackAsync()
    {
        if (!await LeaveCurrentAsync()) return;
        var notes = Notes?.Invoke().ToList() ?? [];
        while (_trail.TryPop(out var path))
        {
            if (notes.FirstOrDefault(n => n.Path == path) is { } previous) { Show(previous); return; }
        }
        IsVisible = false;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private async Task<bool> LeaveCurrentAsync()
    {
        _visual.FinishEdit();
        _autosave.Stop();
        return !(SaveOnClose && HasChanges && !await SaveAsync());
    }

    private void Show(CityNote note)
    {
        _opening = true;
        _edited = false;
        _autosave.Stop();
        _note = note;
        _savedContent = note.Content;
        _title.Text = System.IO.Path.GetFileName(note.Path);
        TouchTab(note.Path);
        Shown?.Invoke(note.Path);
        Editor.Text = note.Content;
        _opening = false;
        RebuildIndex();
        var canVisual = CanVisual(note.Path);
        _segment.IsVisible = canVisual;
        ShowMode(canVisual && StartInVisual ? EditorMode.Visual : EditorMode.Fonte);
        UpdateChrome();
        UpdateStatus(changed: false);
        // renderProps(b) on loadFile; the box is a div, so its line breaks collapse into spaces
        var props = PropertiesFor?.Invoke(note.Path);
        _propertiesText.Text = props is null ? "" : System.Text.RegularExpressions.Regex.Replace(props, @"\s+", " ");
        _hasProperties = props is not null;
        UpdateProperties();
        IsVisible = true;
        // Focus after the overlay is laid out; on Android this brings up the soft keyboard (Fonte; Visual edits on tap).
        if (Mode == EditorMode.Fonte) Dispatcher.UIThread.Post(() => Editor.Focus(), DispatcherPriority.Loaded);
    }

    /// <summary>The "Mais ações" button (v23 overflow): the host shows the note's menu.</summary>
    public event Func<EditorOverlay, Task>? MoreRequested;

    /// <summary>The open note's file was renamed (renomearNota → v21RefreshEditorChrome): the same text,
    /// what was saved and the editor state are kept; only its path and the title change.</summary>
    public void Renamed(string path)
    {
        if (_note is null) return;
        var index = _tabs.IndexOf(_note.Path);
        if (index >= 0) _tabs[index] = path;
        _note = _note with { Path = path };
        _title.Text = System.IO.Path.GetFileName(path);
        RebuildIndex();
        var canVisual = CanVisual(path);
        _segment.IsVisible = canVisual;
        if (!canVisual && Mode == EditorMode.Visual) ShowMode(EditorMode.Fonte);
        UpdateChrome();
        UpdateStatus(changed: HasChanges);
        RenderTabs();
    }

    // ------------------------------------------------------------------ tabs (v21OpenTabs)

    /// <summary>The notes opened, in the order they were first opened (v21OpenTabs).</summary>
    private readonly List<string> _tabs = [];
    private readonly StackPanel _tabRow = new() { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(12, 6) };
    private readonly ScrollViewer _tabBar = new()
    {
        IsVisible = false,
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
    };

    /// <summary>The open tabs (tests and the host).</summary>
    public IReadOnlyList<string> Tabs => _tabs;

    /// <summary>v21TouchTab: tabs of notes that no longer exist go; the note opened joins at the end.</summary>
    private void TouchTab(string path)
    {
        if (Notes?.Invoke().Select(n => n.Path).ToHashSet(StringComparer.Ordinal) is { } existing)
            _tabs.RemoveAll(t => t != path && !existing.Contains(t));
        if (!_tabs.Contains(path)) _tabs.Add(path);
        RenderTabs();
    }

    /// <summary>v21RenderTabs: a tab per note, the open one marked, each with ×; the bar shows from two tabs on (v22Single).</summary>
    private void RenderTabs()
    {
        _tabBar.Content ??= _tabRow;
        _tabRow.Children.Clear();
        foreach (var path in _tabs)
        {
            bool active = path == _note?.Path;
            var close = new Button
            {
                Content = UrbeTheme.Icon(UrbeTheme.Icons.CloseUi, 12, active ? UrbeTheme.Text : UrbeTheme.Text3, 2),
                Width = 22, Height = 22, Padding = new Thickness(0), CornerRadius = new CornerRadius(11), Background = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(close, "Fechar aba");
            var tab = path;
            close.Click += async (_, e) => { e.Handled = true; await CloseTabAsync(tab); };
            var label = new TextBlock
            {
                Text = System.IO.Path.GetFileName(path), FontFamily = UrbeTheme.UiFont, FontSize = 13, MaxWidth = 180,
                FontWeight = active ? FontWeight.SemiBold : FontWeight.Normal, TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = UrbeTheme.Brush(active ? UrbeTheme.Text : UrbeTheme.Text2), VerticalAlignment = VerticalAlignment.Center
            };
            var button = new Button
            {
                Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { label, close } },
                Height = 32, Padding = new Thickness(12, 0, 6, 0), CornerRadius = new CornerRadius(16),
                Background = UrbeTheme.Brush(active ? UrbeTheme.Surface3 : UrbeTheme.Surface2),
                BorderBrush = UrbeTheme.Brush(active ? UrbeTheme.Accent : UrbeTheme.Line2), BorderThickness = new Thickness(1),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            Avalonia.Automation.AutomationProperties.SetName(button, "Aba " + System.IO.Path.GetFileName(path));
            button.Click += async (_, _) => await SwitchTabAsync(tab);
            _tabRow.Children.Add(button);
        }
        _tabBar.IsVisible = _tabs.Count >= 2;
    }

    /// <summary>A tab tapped: the open note is saved, then the tab's note opens.</summary>
    public async Task SwitchTabAsync(string path)
    {
        if (path == _note?.Path) return;
        if (Notes?.Invoke().FirstOrDefault(n => n.Path == path) is not { } note) { _tabs.Remove(path); RenderTabs(); return; }
        if (!await LeaveCurrentAsync()) return;
        _trail.Clear();
        Show(note);
    }

    /// <summary>× of a tab: it closes; if it was the open note, the last tab opens, or the editor closes when none is left.</summary>
    public async Task CloseTabAsync(string path)
    {
        if (path != _note?.Path)
        {
            _tabs.Remove(path);
            RenderTabs();
            return;
        }
        if (!await LeaveCurrentAsync()) return;
        _tabs.Remove(path);
        var notes = Notes?.Invoke().ToList() ?? [];
        while (_tabs.Count > 0)
        {
            var next = _tabs[^1];
            if (notes.FirstOrDefault(n => n.Path == next) is { } note) { _trail.Clear(); Show(note); return; }
            _tabs.RemoveAt(_tabs.Count - 1);
        }
        RenderTabs();
        IsVisible = false;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Text currently in the editor, as a note.</summary>
    public CityNote? Current => _note is null ? null : _note with { Content = Editor.Text ?? "" };

    /// <summary>Personalização → editor: largura (estreita 600, media 720, larga 920, total) and modoInicial.</summary>
    public void ApplyOptions(string? width, string? initialMode)
    {
        var max = width switch { "estreita" => 600.0, "larga" => 920.0, "total" => double.PositiveInfinity, _ => 720.0 };
        _sourceColumn.MaxWidth = max;
        _visual.MaxWidth = max;
        StartInVisual = initialMode != "texto";
    }

    private static bool CanVisual(string path) => Kind(path) is "md" or "markdown";
    private static string Kind(string path) => System.IO.Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
    private bool IsMarkdown => _note is not null && CanVisual(_note.Path);

    private bool _hasProperties;

    /// <summary>#editorScroll.previewMode #propertiesBox{display:none}.</summary>
    private void UpdateProperties() => _properties.IsVisible = _hasProperties && Mode == EditorMode.Fonte;

    public void SetMode(EditorMode mode) => ShowMode(mode == EditorMode.Visual && (_note is null || CanVisual(_note.Path)) ? EditorMode.Visual : EditorMode.Fonte);

    private void ShowMode(EditorMode mode)
    {
        if (Mode == EditorMode.Visual && mode != EditorMode.Visual) _visual.FinishEdit();   // serialise what was edited
        Mode = mode;
        ShowActiveTools(new HashSet<string>());
        UpdateActiveTools();
        UpdateProperties();
        MathBar?.Update();
        var visual = mode == EditorMode.Visual;
        _visualScroll.IsVisible = visual;
        _sourceScroll.IsVisible = !visual;
        if (visual) _visual.SetText(Editor.Text ?? "");
        else _wiki.Hide();
        if (_visualButton is not null)
        {
            Style(_visualButton, visual);
            Style(_sourceButton, !visual);
        }
        UpdateChrome();
        if (!visual && IsVisible) Dispatcher.UIThread.Post(() => Editor.Focus(), DispatcherPriority.Loaded);
    }

    // ------------------------------------------------------------------ layout

    private const int ToolbarRowTop = 2, ToolbarRowBottom = 5;

    /// <summary>Below this width (a phone) the formatting bar sits at the bottom, next to the keyboard (DEC-0045, UC-37).</summary>
    public const double ToolbarBottomBelow = 720;

    /// <summary>Whether the formatting bar is on the bottom row, next to the soft keyboard.</summary>
    public bool ToolbarAtBottom => GetRow(_toolbarHost) == ToolbarRowBottom;

    // The 1.8.4 kept #mdToolbar at the top, far from the thumbs while typing on the phone (owner, DEC-0045).
    private void PlaceToolbar(double width)
    {
        var bottom = width > 0 && width < ToolbarBottomBelow;
        SetRow(_toolbarHost, bottom ? ToolbarRowBottom : ToolbarRowTop);
        _toolbarHost.BorderThickness = bottom ? new Thickness(0, 1, 0, 0) : new Thickness(0, 0, 0, 1);
    }

    private void AddRow(Control control, int row)
    {
        SetRow(control, row);
        Children.Add(control);
    }

    private ScrollViewer Column(Control content)
    {
        var host = new Border { Child = content, Padding = new Thickness(20, 28, 20, 48), HorizontalAlignment = HorizontalAlignment.Stretch };
        var scroll = new ScrollViewer
        {
            Content = host,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        TouchTextScroll.Attach(scroll);
        return scroll;
    }

    private Control TopBar()
    {
        var back = IconButton(UrbeTheme.Icons.Back, "Voltar");
        back.Click += async (_, _) => await BackAsync();
        _title.FontFamily = UrbeTheme.UiFont;
        _title.FontWeight = FontWeight.SemiBold;
        _title.FontSize = 16;
        _title.Foreground = UrbeTheme.Brush(UrbeTheme.Text);
        _title.VerticalAlignment = VerticalAlignment.Center;
        _title.TextTrimming = TextTrimming.CharacterEllipsis;

        _visualButton = Segment("Visual", active: true);
        _sourceButton = Segment("Fonte", active: false);
        _visualButton.Click += (_, _) => SetMode(EditorMode.Visual);
        _sourceButton.Click += (_, _) => SetMode(EditorMode.Fonte);
        _segment = new Border
        {
            Background = UrbeTheme.Brush(UrbeTheme.Surface2),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(3),
            Height = 36,
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { _visualButton, _sourceButton } }
        };
        var bar = new DockPanel { Margin = new Thickness(8, 0, 12, 0), LastChildFill = true };
        DockPanel.SetDock(back, Dock.Left);
        // v23: the overflow "Mais ações" after Visual/Fonte (abrirMenuExplorer with the note's actions)
        var more = IconButton(UrbeTheme.Icons.More, "Mais ações");
        more.Click += async (_, _) =>
        {
            _visual.FinishEdit();
            if (MoreRequested is not null && _note is not null) await MoreRequested(this);
        };
        // no Salvar: the 1.8.4 saves by itself (autosave below, Ctrl+S) and says so in the footer
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { _segment, more } };
        DockPanel.SetDock(right, Dock.Right);
        bar.Children.Add(back);
        bar.Children.Add(right);
        bar.Children.Add(_title);
        _title.Margin = new Thickness(6, 0);
        return new Border
        {
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = bar
        };
    }

    // #mdToolbar (Markdown, in Fonte and in Visual, where it acts on the block of the caret) or .v21CodeTools (other text files, Fonte)
    private Control Toolbars()
    {
        foreach (var button in LegacyEditorTools.Toolbar)
        {
            _markdownTools.Children.Add(ToolButton(button));
            if (LegacyEditorTools.SeparatorAfter.Contains(button.Id))
                _markdownTools.Children.Add(new Border { Width = 1, Height = 20, Margin = new Thickness(6, 0), Background = UrbeTheme.Brush(UrbeTheme.Line2) });
        }
        // math/editor.js: a separator and the ∑ button (mdMath) at the end of the bar
        _markdownTools.Children.Add(new Border { Width = 1, Height = 20, Margin = new Thickness(6, 0), Background = UrbeTheme.Brush(UrbeTheme.Line2) });
        var math = new Button
        {
            Content = new TextBlock { Text = "∑", FontFamily = new FontFamily("Georgia, serif"), FontWeight = FontWeight.SemiBold, FontSize = 16 },
            Width = 40, Height = 40, Padding = new Thickness(0), CornerRadius = new CornerRadius(8), Focusable = false,
            Background = Brushes.Transparent, Foreground = UrbeTheme.Brush(UrbeTheme.Text2),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(math, "Fórmula (LaTeX) — Ctrl+M");
        math.Click += async (_, _) => await StartFormulaAsync(false);
        _markdownTools.Children.Add(math);
        foreach (var (label, run) in new (string, Action)[]
                 {
                     ("⇥ Indentar", () => ApplyEdit(LegacyEditorTools.IndentSelection(Editor.Text ?? "", Editor.SelectionStart, Editor.SelectionEnd))),
                     ("{ } Formatar", FormatCurrent)
                 })
        {
            var b = new Button
            {
                Content = label, Height = 36, Padding = new Thickness(12, 0), CornerRadius = new CornerRadius(8), Focusable = false,
                Background = UrbeTheme.Brush(UrbeTheme.Surface2), Foreground = UrbeTheme.Brush(UrbeTheme.Text2), FontSize = 13, FontWeight = FontWeight.SemiBold
            };
            b.Click += (_, _) => run();
            _codeTools.Children.Add(b);
        }
        var scroll = new ScrollViewer
        {
            Content = new Grid { Children = { _markdownTools, _codeTools } },
            // #mdToolbar{scrollbar-width:none}: it scrolls sideways with no bar
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalContentAlignment = VerticalAlignment.Center,
            Height = 52
        };
        _toolbarHost.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line);
        _toolbarHost.BorderThickness = new Thickness(0, 0, 0, 1);
        _toolbarHost.Child = scroll;
        return _toolbarHost;
    }

    private Control ToolButton(LegacyEditorTools.ToolbarButton tool)
    {
        Control content = tool.Id switch
        {
            "h1" => Label("H1"), "h2" => Label("H2"), "h3" => Label("H3"),
            "bold" => new TextBlock { Text = "B", FontWeight = FontWeight.ExtraBold, FontSize = 14 },
            "italic" => new TextBlock { Text = "I", FontFamily = new FontFamily("Georgia, serif"), FontWeight = FontWeight.SemiBold, FontStyle = FontStyle.Italic, FontSize = 14 },
            _ => UrbeTheme.Icon(SlashMenu.Icons[tool.Icon], 20, UrbeTheme.Text2)
        };
        var b = new Button
        {
            Content = content, Width = 40, Height = 40, Padding = new Thickness(0), CornerRadius = new CornerRadius(8), Focusable = false,
            Background = Brushes.Transparent, Foreground = UrbeTheme.Brush(UrbeTheme.Text2),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(b, tool.Title);
        b.Click += async (_, _) => await RunToolAsync(tool);
        _toolButtons[tool.Id] = b;
        return b;
        static TextBlock Label(string t) => new() { Text = t, FontWeight = FontWeight.Bold, FontSize = 14 };
    }

    private readonly Dictionary<string, Button> _toolButtons = new(StringComparer.Ordinal);

    /// <summary>The toolbar buttons that show as on now (tests and the host).</summary>
    public IReadOnlySet<string> ActiveTools { get; private set; } = new HashSet<string>();

    // Fonte: the state of the buttons is the Markdown under the caret (DEC-0045, UC-37)
    private void UpdateActiveTools()
    {
        if (Mode != EditorMode.Fonte) return;
        var text = Editor.Text ?? "";
        ShowActiveTools(LegacyEditorTools.ActiveTools(text, Editor.SelectionStart, Editor.SelectionEnd));
    }

    private void ShowActiveTools(IReadOnlySet<string> on)
    {
        ActiveTools = on;
        foreach (var (id, button) in _toolButtons)
            button.Background = on.Contains(id) ? UrbeTheme.Brush(UrbeTheme.Accent, .22) : Brushes.Transparent;
    }

    private Control Footer()
    {
        _status.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        _saveState.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        _status.FontSize = _saveState.FontSize = 12;
        _status.VerticalAlignment = _saveState.VerticalAlignment = VerticalAlignment.Center;
        var bar = new DockPanel { Margin = new Thickness(16, 0) };
        DockPanel.SetDock(_saveState, Dock.Right);
        bar.Children.Add(_saveState);
        bar.Children.Add(_status);
        return new Border { BorderBrush = UrbeTheme.Brush(UrbeTheme.Line), BorderThickness = new Thickness(0, 1, 0, 0), Child = bar };
    }

    private void UpdateChrome()
    {
        var fonte = Mode == EditorMode.Fonte;
        // v23AtualizarToolbar: #mdToolbar shows for Markdown in both modes; the code tools only in Fonte
        _toolbarHost.IsVisible = fonte || IsMarkdown;
        _markdownTools.IsVisible = IsMarkdown;
        _codeTools.IsVisible = !IsMarkdown;
    }

    // ------------------------------------------------------------------ text changes, status, autosave

    private bool _edited;

    private void OnTextChanged()
    {
        if (_opening) return;
        if (_note is not null && !string.Equals(Editor.Text, _note.Content, StringComparison.Ordinal)) _edited = true;
        UpdateStatus(changed: true);
        if (!_fromVisual && Mode == EditorMode.Fonte) { UpdateWiki(); UpdateActiveTools(); }
        ScheduleAutosave();
    }

    private void UpdateStatus(bool changed)
    {
        // v21RefreshEditorChrome shows "MD · n linhas" when a note opens; updateStats (on input) adds the words
        var kind = _note is null ? "md" : Kind(_note.Path);
        _status.Text = _edited ? LegacyEditorTools.Stats(Editor.Text ?? "", kind) : LegacyEditorTools.ChromeStats(Editor.Text ?? "", kind);
        // workspace:dirty → 'Alterado', workspace:saving → 'Salvando...', workspace:saved → 'Salvo'
        _saveState.Text = _saving ? "Salvando..." :
            SaveOnClose ? (HasChanges ? "Alterado" : "Salvo") :
            (changed ? "Alterado (prévia: não grava no vault)" : "Prévia: não grava no vault");
    }

    private void ScheduleAutosave()
    {
        if (!SaveOnClose || SaveRequested is null || _note is null || !HasChanges) return;
        if (!_autosave.IsEnabled) _autosave.Start();
    }

    private async Task AutosaveTickAsync()
    {
        _autosave.Stop();
        if (!HasChanges) return;
        if (!_saving && await SaveAsync() && HasChanges) ScheduleAutosave();
        else if (_saving) ScheduleAutosave();
    }

    /// <summary>Save only via the native vault adapter, never silently to an in-memory preview.</summary>
    public async Task<bool> SaveAsync()
    {
        // a save already running (the autosave) is waited for, then what changed since is saved too
        bool waited = false;
        while (_saving && _saveRun is { } running) { await running; waited = true; }
        if (_saving || Current is not { } note) return false;
        if (waited && !HasChanges) return true;
        var done = new TaskCompletionSource();
        _saveRun = done.Task;
        try { return await SaveNowAsync(note); }
        finally { done.SetResult(); }
    }

    private Task? _saveRun;

    private async Task<bool> SaveNowAsync(CityNote note)
    {
        if (SaveRequested is null)
        {
            _saveState.Text = "Sem armazenamento conectado";
            return false;
        }
        _saving = true;
        _saveState.Text = "Salvando...";
        try
        {
            await SaveRequested(note);
            _savedContent = note.Content;
            _saveState.Text = "Salvo";
            ToolTip.SetTip(_saveState, "Salvo e verificado no vault");
            return true;
        }
        catch (Exception ex)
        {
            // workspace:saveError → 'Erro ao salvar'; the reason stays on the label
            _saveState.Text = "Erro ao salvar";
            ToolTip.SetTip(_saveState, ex.Message);
            return false;
        }
        finally { _saving = false; }
    }

    /// <summary>A short message in the status line (toast of the editor), then the status again.</summary>
    public void Notify(string message)
    {
        _saveState.Text = message;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2600) };
        timer.Tick += (_, _) => { timer.Stop(); UpdateStatus(changed: false); };
        timer.Start();
    }

    // ------------------------------------------------------------------ Fonte tools (app.js .mdBtn handlers)

    private void ApplyEdit(LegacyEditorTools.TextEdit edit)
    {
        Editor.Text = edit.Text;
        Editor.SelectionStart = edit.SelectionStart;
        Editor.SelectionEnd = edit.SelectionEnd;
        Editor.CaretIndex = edit.SelectionEnd;
        Editor.Focus();
    }

    private async Task RunToolAsync(LegacyEditorTools.ToolbarButton tool)
    {
        // v23: in the Visual mode the bar acts on the block of the caret
        if (Mode == EditorMode.Visual) { _visual.Toolbar(tool.Id); return; }
        var text = Editor.Text ?? "";
        var (a, b) = (Editor.SelectionStart, Editor.SelectionEnd);
        if (tool.Special == "noteLink")
        {
            var names = Notes?.Invoke().Where(n => n.Path != _note?.Path).Select(n => n.Name).ToList() ?? [];
            if (names.Count == 0 || Dialogs is null)
            {
                if (Dialogs is { } d) await d.AlertAsync("Vincular nota", "Crie outra nota para poder vinculá-la.");
                return;
            }
            var pick = await Dialogs.ChooseAsync("Vincular nota", names.Select(n => new DialogItem(n, "file")).ToList());
            if (pick is not { } i) return;
            ApplyEdit(LegacyEditorTools.NoteLink(Editor.Text ?? text, a, b, names[i]));
            return;
        }
        if (LegacyEditorTools.ApplySource(tool, text, a, b) is { } edit) ApplyEdit(edit);
    }

    private void FormatCurrent()
    {
        if (_note is null) return;
        var (result, message) = LegacyEditorTools.Format(Editor.Text ?? "", Kind(_note.Path));
        if (result is not null) Editor.Text = result;
        if (message.Length > 0) Notify(message);
    }

    // ------------------------------------------------------------------ [[ suggestions

    private void RebuildIndex()
    {
        _index?.Dispose();
        _index = null;
        _store = null;
        _currentId = null;
        if (Notes is null) return;
        var store = new DocumentStore();
        foreach (var n in Notes().Where(n => CanVisual(n.Path)))
            store.Upsert(new DocumentInput { Path = n.Path, Content = n.Path == _note?.Path ? _note.Content : n.Content });
        _store = store;
        _currentId = _note is null ? null : store.Get(_note.Path)?.Id;
        _index = new KnowledgeIndex(store);
    }

    /// <summary>wikiCandidates(query): knowledge.search(query, 20) without the open note; names are the document titles.</summary>
    private IReadOnlyList<string> Candidates(string query) =>
        _index is null ? [] : _index.Search(query, 20).Where(d => d.Id != _currentId).Select(d => d.Title).ToArray();

    private void UpdateWiki()
    {
        if (Mode != EditorMode.Fonte) return;
        var ctx = LegacyEditorTools.FindWikiContext(Editor.Text ?? "", Editor.CaretIndex);
        if (ctx is null) _wiki.Hide(); else _wiki.Show(Candidates(ctx.Query));
    }

    private void CommitWiki(string name)
    {
        if (LegacyEditorTools.CommitWiki(Editor.Text ?? "", Editor.CaretIndex, name) is { } edit) ApplyEdit(edit);
        _wiki.Hide();
    }

    private async Task<string?> AskUrlAsync() =>
        Dialogs is null ? null : await Dialogs.PromptAsync(new PromptOptions("Inserir link", "Endereço", "https://", Confirm: "Inserir",
            Validate: v => LegacyVisualEdit.IsValidUrl(v) ? null : "Use um endereço começando com https://"));

    /// <summary>bindPreviewWikiLinks: open the note a wikilink names (exact path/title, else the single case-insensitive title match).</summary>
    public async Task FollowLinkAsync(string name)
    {
        var all = Notes?.Invoke().ToList() ?? [];
        var exact = all.FirstOrDefault(n => string.Equals(n.Path, name, StringComparison.Ordinal) || string.Equals(n.Path, name + ".md", StringComparison.Ordinal));
        var matches = all.Where(n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
        var target = exact ?? (matches.Count == 1 ? matches[0] : null);
        if (target is not null) { await NavigateAsync(target); return; }
        if (matches.Count > 1) { Notify("Link ambíguo: use o caminho da nota."); return; }
        // The 1.8.4 only said the note did not exist (owner, DEC-0045): offer to create it, next to this one.
        if (CreateLinked is null || Dialogs is null || !LegacyEditorTools.IsCreatableLinkName(name)) { Notify("A nota vinculada não existe."); return; }
        if (!await Dialogs.ConfirmAsync("Criar nota", $"A nota “{name}” ainda não existe. Criar agora?", "Criar nota")) return;
        if (!await LeaveCurrentAsync()) return;
        if (await CreateLinked(name) is { } made) Go(made);
    }

    // ------------------------------------------------------------------ small controls

    private static Button IconButton(string icon, string tip)
    {
        var b = new Button
        {
            // #editorTop .iconBtn: 44×44, radius 12, text-2, a 22px icon of stroke 1.8
            Content = UrbeTheme.Icon(icon == UrbeTheme.Icons.More ? UrbeTheme.Icons.MoreUi : icon, 22, UrbeTheme.Text2, 1.8),
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(12),
            Background = Brushes.Transparent,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(b, tip);
        return b;
    }

    private static Button Segment(string text, bool active)
    {
        var b = new Button
        {
            Content = text,
            Height = 30,
            Padding = new Thickness(12, 0),
            CornerRadius = new CornerRadius(7),
            FontWeight = FontWeight.SemiBold,
            FontSize = 13,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        Style(b, active);
        return b;
    }

    private static void Style(Button b, bool active)
    {
        b.Background = active ? UrbeTheme.Brush(UrbeTheme.Surface3) : Brushes.Transparent;
        b.Foreground = UrbeTheme.Brush(active ? UrbeTheme.Text : UrbeTheme.Text3);
    }
}
