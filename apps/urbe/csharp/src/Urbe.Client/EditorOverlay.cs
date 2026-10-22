using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Urbe.Client.Ui;
using Urbe.Core;
using C = Urbe.Client.UrbeTokens.Color;

namespace Urbe.Client;

public enum EditorMode { Visual, Fonte }

/// <summary>
/// The 1.8.4 full editor (#editorFull) over the Explorer: top bar with back, file name and Visual/Fonte, the Markdown
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
    private readonly Border _saveDot = new();
    private readonly UrbeToast _toast = new(top: 64);
    private Border _header = null!;
    private Border _footer = null!;
    private Button _titleButton = null!;
    private Border _done = null!;
    private readonly VisualSurface _visual = new();
    private readonly WikiSuggestList _wiki = new();
    private readonly ScrollViewer _visualScroll;
    private readonly ScrollViewer _sourceScroll;
    private readonly StackPanel _sourceColumn;
    private readonly Border _properties;
    private readonly TextBlock _propertiesText = new();

    /// <summary>propertiesFor(b) of the open note, as renderProps writes it ("---\ntítulo: …\n---"), or null when the
    /// file is not a note of the vault; the host supplies it.</summary>
    public Func<string, string?>? PropertiesFor { get; set; }
    private readonly Border _toolbarHost = new();
    private readonly StackPanel _markdownTools = new() { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(8, 0) };
    private readonly StackPanel _codeTools = new() { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(10, 0) };
    private Button _visualButton = null!, _sourceButton = null!;
    private Border _segment = null!;
    private CityNote? _note;
    private string _savedContent = string.Empty;
    private bool _saving, _fromVisual, _opening;
    // UC-48: the autosave waits for a pause in typing (each save lists, plans and verifies the vault on
    // Android), but never holds an edit longer than AutosaveMaxWait while typing goes on.
    private readonly DispatcherTimer _autosave = new() { Interval = AutosavePause };
    internal static readonly TimeSpan AutosavePause = TimeSpan.FromMilliseconds(900);
    internal static readonly TimeSpan AutosaveMaxWait = TimeSpan.FromSeconds(3);
    private DateTime _pendingSince;
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

    /// <summary>The selected text (of the block in Visual, of the text in Fonte), or null.</summary>
    public string? SelectedText
    {
        get
        {
            if (_note is null) return null;
            if (Mode == EditorMode.Visual) return _visual.SelectedText;
            var all = Editor.Text ?? "";
            int a = Math.Clamp(Math.Min(Editor.SelectionStart, Editor.SelectionEnd), 0, all.Length);
            int b = Math.Clamp(Math.Max(Editor.SelectionStart, Editor.SelectionEnd), 0, all.Length);
            return b > a ? all[a..b] : null;
        }
    }

    /// <summary>Pins the selected text (of the block in Visual, of the text in Fonte), else the whole note.</summary>
    public void PinSelectionOrNote()
    {
        if (_note is null) return;
        var text = SelectedText;
        if (string.IsNullOrWhiteSpace(text)) text = Editor.Text ?? "";
        Pinned.Pin(text, _note.Path);
    }

    /// <summary>
    /// Personalização → Editor and Texto: the text column width, the Fonte font size, the line height factor and the size
    /// of the notes in Visual (--ui-editor-width, --ui-code-size, --ui-editor-line, --ui-editor-size: "Tamanho do texto").
    /// </summary>
    public void ApplyAppearance(double columnWidth, double codeSize, double lineHeight, double? textSize = null)
    {
        Editor.MaxWidth = columnWidth;
        Editor.FontSize = codeSize;
        Editor.LineHeight = codeSize * lineHeight;
        _visual.ApplyText(textSize ?? _visual.TextSize, lineHeight);
        FitReadingColumn();
    }

    public EditorOverlay()
    {
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        // top bar, find, toolbar (wide), text, footer, toolbar (narrow: on the last row, right above the soft keyboard)
        RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto,Auto");
        Editor = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = false,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = UrbeTheme.MonoFont,
            FontSize = 15,
            LineHeight = 15 * 1.7,
            Foreground = UrbeTheme.Brush(UrbeTheme.Text, MarkdownReader.BodyInk),
            FontWeight = FontWeight.Normal,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CaretBrush = UrbeTheme.Brush(UrbeTheme.Accent),
            SelectionBrush = UrbeTheme.Brush(UrbeTheme.Accent, .35),
            SelectionForegroundBrush = UrbeTheme.Brush(UrbeTheme.Text),
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
        AutomationProperties.SetName(Editor, "Texto da nota");
        Editor.TextChanged += (_, _) => { OnTextChanged(); KeepSourceCaretSoon(typing: true); };
        Editor.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.CaretIndexProperty) { UpdateWiki(); KeepSourceCaretSoon(typing: false); }
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
        _visual.EditingChanged += _ => UpdateChrome();
        _visual.WikiCandidates = Candidates;
        _visual.AskUrl = AskUrlAsync;
        _visual.AskBox = AskBoxAsync;
        _visual.AskComment = comments => Dialogs?.CommentsAsync("Comentários", comments) ?? Task.FromResult<string?>(null);

        AddRow(_header = TopBar(), 0);
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
        // the header gives the text room while scrolling down and comes back on the way up; the caret stays in sight
        // when the visible area changes (the keyboard opens)
        _visualScroll.ScrollChanged += (_, e) => OnScrolled(_visualScroll, e);
        _sourceScroll.ScrollChanged += (_, e) => OnScrolled(_sourceScroll, e);
        _visualScroll.SizeChanged += (_, e) => { if (e.HeightChanged) _visual.KeepCaretSoon(typing: true); };
        _sourceScroll.SizeChanged += (_, e) => { if (e.HeightChanged) KeepSourceCaretSoon(typing: true); };
        // #mathDock: the formula bar of Fonte, over the bottom of the text
        MathBar = new MathDock(Editor, () => Mode == EditorMode.Fonte && IsVisible);
        var text = new Grid { Children = { _sourceScroll, _visualScroll, MathBar, Context, Connections } };
        text.SizeChanged += (_, e) => { MathBar.Place(e.NewSize.Width); Context.Place(e.NewSize.Width); Connections.Place(e.NewSize.Width); };
        // one side panel at a time
        Context.PropertyChanged += (_, e) => { if (e.Property == IsVisibleProperty && Context.IsVisible) Connections.IsVisible = false; };
        Connections.PropertyChanged += (_, e) => { if (e.Property == IsVisibleProperty && Connections.IsVisible) Context.IsVisible = false; };
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
        AddRow(_footer = Footer(), 4);
        // saving errors and short notices: a discreet toast over the text, never a dialog (UX.md P3)
        SetRowSpan(_toast, 6);
        Children.Add(_toast);
        _autosave.Tick += async (_, _) => await AutosaveTickAsync();
        ApplyOptions(null, null);
        ShowMode(EditorMode.Visual);
    }

    public TextBox Editor { get; }
    public event EventHandler? Closed;

    /// <summary>A note was opened in the editor (by any way: Notas, a search, a link, a tab).</summary>
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
        var (a, b) = VisualSurface.Range(Editor);
        var txt = text[a..b];
        var pre = block && a > 0 && text[a - 1] != '\n' ? "\n" : "";
        var sn = MathEditing.Snippet(block ? pre + "$$\n" + (txt.Length > 0 ? txt : "●") + "\n$$\n" : "$" + (txt.Length > 0 ? txt : "●") + "$");
        ApplyEdit(new LegacyEditorTools.TextEdit(text[..a] + sn.Text + text[b..], a + sn.Caret, a + sn.Caret));
    }

    /// <summary>The formula bar of Fonte (math/editor.js #mathDock).</summary>
    public MathDock MathBar { get; }

    /// <summary>"Estrutura e backlinks" (editor/chrome.js .uec-context).</summary>
    public EditorContextPanel Context { get; } = new();

    /// <summary>"Conexões": the local graph of the note (UC-71, ADD-0029).</summary>
    public ConnectionsPanel Connections { get; } = new();

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

    /// <summary>Opens a note fresh (from Notas, a search…): the trail of followed links starts over.</summary>
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
        var saving = LeaveCurrentAsync();
        Go(target);
        await saving;
    }

    /// <summary>Notes Voltar went back from, for Avançar (editor.forward); following a new link clears it.</summary>
    private readonly Stack<string> _forward = new();

    /// <summary>Avançar is possible.</summary>
    public bool CanForward => _forward.Count > 0;

    /// <summary>editor.forward: the note Voltar left, if it still exists.</summary>
    public async Task ForwardAsync()
    {
        var saving = LeaveCurrentAsync();
        var notes = Notes?.Invoke().ToList() ?? [];
        while (_forward.TryPop(out var path))
        {
            if (notes.FirstOrDefault(n => n.Path == path) is not { } next) continue;
            if (_note is { } current) _trail.Push(current.Path);
            Show(next);
            break;
        }
        await saving;
    }

    /// <summary>editor.undo / editor.redo: of the text being edited (Fonte, or the block in Visual).</summary>
    public void Undo() { if (Mode == EditorMode.Fonte) Editor.Undo(); else _visual.Undo(); }
    public void Redo() { if (Mode == EditorMode.Fonte) Editor.Redo(); else _visual.Redo(); }

    private void Go(CityNote target)
    {
        _forward.Clear();
        if (_note is { } current && current.Path != target.Path) _trail.Push(current.Path);
        Show(target.Path == _note?.Path ? Current! : target);
    }

    /// <summary>Voltar: the previous note of the trail, or closes back to where the editor was opened from.</summary>
    public async Task BackAsync()
    {
        var saving = LeaveCurrentAsync();
        var notes = Notes?.Invoke().ToList() ?? [];
        while (_trail.TryPop(out var path))
        {
            if (notes.FirstOrDefault(n => n.Path == path) is { } previous)
            {
                if (_note is { } current) _forward.Push(current.Path);
                Show(previous);
                await saving;
                return;
            }
        }
        // the Explorer is back at once; the note finishes saving behind it
        IsVisible = false;
        Closed?.Invoke(this, EventArgs.Empty);
        await saving;
    }

    /// <summary>
    /// Leaving the open note never waits on the screen (UX.md P2): the edit is saved — through the same verified path —
    /// while the next note or the Explorer is already showing. The task ends when the save does (the host and tests can
    /// wait for it). A save that fails keeps the text: the note shows it again, still to be saved, and a toast offers to
    /// try again.
    /// </summary>
    private Task LeaveCurrentAsync()
    {
        _visual.FinishEdit();
        _autosave.Stop();
        if (!SaveOnClose || !HasChanges || Current is not { } note) return Task.CompletedTask;
        return SaveLeftAsync(note, _savedContent);
    }

    private async Task SaveLeftAsync(CityNote note, string previous)
    {
        if (await SaveSnapshotAsync(note)) return;
        // the text that did not reach the vault, and what the vault has, for the next open of this note
        if (!_failed.ContainsKey(note.Path)) _failed[note.Path] = (note, previous);
        else _failed[note.Path] = (note, _failed[note.Path].Previous);
    }

    /// <summary>Notes whose last save failed after the editor left them: their text and the text the vault still has.</summary>
    private readonly Dictionary<string, (CityNote Note, string Previous)> _failed = new(StringComparer.Ordinal);

    /// <summary>Notes whose text is not in the vault yet (a save failed after leaving them).</summary>
    public IReadOnlyCollection<string> UnsavedNotes => _failed.Keys;

    /// <summary>"Tentar de novo": saves again the notes whose save failed (the open one included).</summary>
    public async Task RetrySavesAsync()
    {
        foreach (var (path, (note, _)) in _failed.ToList())
        {
            if (path == _note?.Path) continue;
            if (await SaveSnapshotAsync(note)) _failed.Remove(path);
        }
        if (_note is not null && HasChanges) await SaveAsync();
    }

    /// <summary>A save failed while the editor was closed: the host shows it (message), with "Tentar de novo".</summary>
    public event Action<string>? SaveFailed;

    private void Show(CityNote note)
    {
        _opening = true;
        _edited = false;
        _autosave.Stop();
        _saveError = null;
        // a note whose save failed comes back with the text that was not saved, still pending
        var pendingFrom = (string?)null;
        if (_failed.Remove(note.Path, out var failed)) { note = note with { Content = failed.Note.Content }; pendingFrom = failed.Previous; }
        _note = note;
        _savedContent = pendingFrom ?? note.Content;
        ShowHeader(animate: false);
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
        RefreshProperties();
        IsVisible = true;
        if (pendingFrom is not null) ScheduleAutosave();
        // Focus after the overlay is laid out; on Android this brings up the soft keyboard (Fonte; Visual edits on tap).
        if (Mode == EditorMode.Fonte) Dispatcher.UIThread.Post(FocusSource, DispatcherPriority.Loaded);
    }

    /// <summary>The Fonte text gets the focus and the phone's keyboard comes up (a focus set by the app does not always bring it).</summary>
    private void FocusSource()
    {
        if (!IsVisible || Mode != EditorMode.Fonte) return;
        Editor.Focus();
        PlatformInput.ShowKeyboard?.Invoke();
    }

    /// <summary>The title was tapped: the host renames the note (renomearNota).</summary>
    public event Action? RenameRequested;

    /// <summary>The "Mais ações" button (v23 overflow): the host shows the note's menu.</summary>
    public event Func<EditorOverlay, Task>? MoreRequested;

    /// <summary>The Assistente button of the top bar (owner, UC-48: "não está com o botão da IA").</summary>
    public event Action? AssistantRequested;

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

    /// <summary>editor.session.restore: the tabs of a session (the existing notes), the active one shown.</summary>
    public void RestoreSession(IReadOnlyList<string> tabs, string? active)
    {
        var notes = Notes?.Invoke().ToList() ?? [];
        foreach (var t in tabs) if (notes.Any(n => n.Path == t) && !_tabs.Contains(t)) _tabs.Add(t);
        var show = notes.FirstOrDefault(n => n.Path == active) ?? notes.FirstOrDefault(n => n.Path == _tabs.LastOrDefault());
        if (show is null) { RenderTabs(); return; }
        IsVisible = true;
        Show(show);
    }

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
        var saving = LeaveCurrentAsync();
        _trail.Clear();
        Show(note);
        await saving;
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
        var saving = LeaveCurrentAsync();
        _tabs.Remove(path);
        var notes = Notes?.Invoke().ToList() ?? [];
        while (_tabs.Count > 0)
        {
            var next = _tabs[^1];
            if (notes.FirstOrDefault(n => n.Path == next) is { } note) { _trail.Clear(); Show(note); await saving; return; }
            _tabs.RemoveAt(_tabs.Count - 1);
        }
        RenderTabs();
        IsVisible = false;
        Closed?.Invoke(this, EventArgs.Empty);
        await saving;
    }

    /// <summary>Text currently in the editor, as a note.</summary>
    public CityNote? Current => _note is null ? null : _note with { Content = Editor.Text ?? "" };

    /// <summary>Personalização → editor: largura (estreita 600, media 720, larga 920, total) and modoInicial.</summary>
    public void ApplyOptions(string? width, string? initialMode)
    {
        var max = width switch { "estreita" => 600.0, "larga" => 920.0, "total" => double.PositiveInfinity, _ => 720.0 };
        _sourceColumn.MaxWidth = max;
        _columnWidth = width ?? "media";
        _columnMax = max;
        FitReadingColumn();
        StartInVisual = initialMode != "texto";
    }

    private string _columnWidth = "media";
    private double _columnMax = 720;

    /// <summary>
    /// The reading column of Visual: on a large screen the default ("Média") keeps lines at about 70 characters, centred,
    /// like a book page; the other widths are the person's choice and are kept.
    /// </summary>
    private void FitReadingColumn() =>
        _visual.MaxWidth = _columnWidth == "media" ? Math.Min(_columnMax, Math.Round(_visual.TextSize * 38)) : _columnMax;

    private static bool CanVisual(string path) => Kind(path) is "md" or "markdown";
    private static string Kind(string path) => System.IO.Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
    private bool IsMarkdown => _note is not null && CanVisual(_note.Path);

    private bool _hasProperties;

    /// <summary>renderProps(b) on loadFile; the box is a div, so its line breaks collapse into spaces. Asked again once
    /// the vault's notes are known (a note restored at the opening).</summary>
    public void RefreshProperties()
    {
        var props = _note is null ? null : PropertiesFor?.Invoke(_note.Path);
        _propertiesText.Text = props is null ? "" : System.Text.RegularExpressions.Regex.Replace(props, @"\s+", " ");
        _hasProperties = props is not null;
        UpdateProperties();
    }

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
        if (!visual && IsVisible) Dispatcher.UIThread.Post(FocusSource, DispatcherPriority.Loaded);
        // the text switching modes fades in briefly (≤ 150 ms): it is the same note, seen another way
        if (IsVisible && !UrbeTokens.Motion.Reduced) UrbeMotion.Enter(visual ? _visualScroll : _sourceScroll, 0, 0, UrbeTokens.Motion.Fast);
    }

    // ------------------------------------------------------------------ the header that gives way while scrolling

    /// <summary>The header's height (56 dp, the 1.8.4 #editorTop).</summary>
    public const double HeaderHeight = 56;

    /// <summary>The header is folded away (the reader scrolled down).</summary>
    public bool HeaderHidden { get; private set; }

    private double _scrollRun;
    private long _headerMovedAt;
    private bool _pointerOnText;
    private long _userScrollAt = -10_000;

    /// <summary>A scroll caused by the reader: a finger or the mouse on the text, the glide after it, or the wheel.</summary>
    private bool UserScrolling => _pointerOnText || Environment.TickCount64 - _userScrollAt < 1500;

    // only the reader's own scrolling folds the header: the editor's moves (caret, a block kept in place) do not
    private void OnScrolled(ScrollViewer scroll, ScrollChangedEventArgs e)
    {
        var dy = e.OffsetDelta.Y;
        if (dy == 0 || !UserScrolling || EditorScrolling.MovedByEditor(scroll) || Environment.TickCount64 - _headerMovedAt < 320) return;
        if (Math.Sign(dy) != Math.Sign(_scrollRun)) _scrollRun = 0;
        _scrollRun += dy;
        var y = scroll.Offset.Y;
        var room = scroll.Extent.Height - scroll.Viewport.Height;
        if (_scrollRun > 24 && !HeaderHidden && y > HeaderHeight && room - y > HeaderHeight && room > HeaderHeight * 3) HideHeader();
        else if ((_scrollRun < -24 || y <= 0) && HeaderHidden) ShowHeader(animate: true);
    }

    private void HideHeader()
    {
        HeaderHidden = true;
        _headerMovedAt = Environment.TickCount64;
        _scrollRun = 0;
        MoveHeader(0, animate: true);
    }

    private void ShowHeader(bool animate)
    {
        if (_header is null) return;
        if (HeaderHidden) _headerMovedAt = Environment.TickCount64;
        HeaderHidden = false;
        _scrollRun = 0;
        MoveHeader(HeaderHeight, animate);
    }

    private void MoveHeader(double height, bool animate)
    {
        _header.Transitions = animate && !UrbeTokens.Motion.Reduced
            ? [new DoubleTransition { Property = HeightProperty, Duration = UrbeTokens.Motion.Fast, Easing = UrbeTokens.Motion.Standard },
               new DoubleTransition { Property = OpacityProperty, Duration = UrbeTokens.Motion.Fast, Easing = UrbeTokens.Motion.Standard }]
            : null;
        _header.Height = height;
        _header.Opacity = height > 0 ? 1 : 0;
        _header.IsHitTestVisible = height > 0;
    }

    // ------------------------------------------------------------------ the caret in sight (Fonte)

    private bool _sourceKeepPending, _sourceKeepTyping;

    private void KeepSourceCaretSoon(bool typing)
    {
        if (Mode != EditorMode.Fonte || _opening) return;
        _sourceKeepTyping |= typing;
        if (_sourceKeepPending) return;
        _sourceKeepPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _sourceKeepPending = false;
            var t = _sourceKeepTyping;
            _sourceKeepTyping = false;
            if (Mode == EditorMode.Fonte && IsVisible && Editor.IsFocused) EditorScrolling.KeepCaret(Editor, _sourceScroll, t);
        }, DispatcherPriority.Loaded);
    }

    // ------------------------------------------------------------------ layout

    private const int ToolbarRowTop = 2, ToolbarRowBottom = 5;

    /// <summary>Below this width (a phone) the formatting bar sits at the bottom, next to the keyboard (DEC-0045, UC-37).</summary>
    public const double ToolbarBottomBelow = 720;

    /// <summary>Whether the formatting bar is on the bottom row, next to the soft keyboard.</summary>
    public bool ToolbarAtBottom => GetRow(_toolbarHost) == ToolbarRowBottom;

    // The 1.8.4 kept #mdToolbar at the top, far from the thumbs while typing on the phone (owner, DEC-0045). On a phone
    // it is the last row: the app ends where the keyboard starts (MainView.KeyboardCover), so it sits right on the keys.
    private void PlaceToolbar(double width)
    {
        var bottom = width > 0 && width < ToolbarBottomBelow;
        if (bottom == ToolbarAtBottom && _toolbarPlaced) return;
        _toolbarPlaced = true;
        SetRow(_toolbarHost, bottom ? ToolbarRowBottom : ToolbarRowTop);
        _toolbarHost.BorderThickness = bottom ? new Thickness(0, 1, 0, 0) : new Thickness(0, 0, 0, 1);
        _done.IsVisible = bottom;
        UpdateChrome();
    }

    private bool _toolbarPlaced;

    private void AddRow(Control control, int row)
    {
        SetRow(control, row);
        Children.Add(control);
    }

    private ScrollViewer Column(Control content)
    {
        // room below the last line, so it can be written in the upper part of the screen too
        var host = new Border { Child = content, Padding = new Thickness(UrbeTokens.Space.L, UrbeTokens.Space.L, UrbeTokens.Space.L, 160), HorizontalAlignment = HorizontalAlignment.Stretch };
        var scroll = new ScrollViewer
        {
            Content = host,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        TouchTextScroll.Attach(scroll);
        // a tap or a hold that scrolled the note, or only stopped it, is not for the block under the finger (22/10/2026)
        TouchIntent.Attach(scroll, () => EditorScrolling.MovedByEditor(scroll));
        // the reader's own scrolling: a finger or the mouse on the text (and the glide after it), or the wheel
        scroll.AddHandler(PointerPressedEvent, (_, _) => { _pointerOnText = true; _userScrollAt = Environment.TickCount64; }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        scroll.AddHandler(PointerReleasedEvent, (_, _) => { _pointerOnText = false; _userScrollAt = Environment.TickCount64; }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        scroll.AddHandler(PointerCaptureLostEvent, (_, _) => { _pointerOnText = false; _userScrollAt = Environment.TickCount64; }, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        scroll.AddHandler(PointerWheelChangedEvent, (_, _) => _userScrollAt = Environment.TickCount64, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        return scroll;
    }

    /// <summary>
    /// The header (UX.md P4, P5, P9): Voltar, the note's title (a tap renames it) with the save dot, the Assistente, the
    /// Visual/Fonte switch and "Mais ações" — every target 48 dp, tokens only. No Salvar: the note saves by itself.
    /// </summary>
    private Border TopBar()
    {
        var back = new UrbeIconButton(UrbeTheme.Icons.Back, "Voltar", 24, C.TextPrimary);
        back.Click += async (_, _) => await BackAsync();
        UrbeTokens.Type.BodyStrong.Apply(_title);
        _title.LineHeight = double.NaN;
        _title.Foreground = UrbeTheme.Brush(C.TextPrimary);
        _title.VerticalAlignment = VerticalAlignment.Center;
        _title.TextTrimming = TextTrimming.CharacterEllipsis;

        // the save state: a tiny dot after the title — calm when saved, warm while pending, red on an error (P2, P3)
        _saveDot.Width = _saveDot.Height = 8;
        _saveDot.CornerRadius = new CornerRadius(4);
        _saveDot.VerticalAlignment = VerticalAlignment.Center;
        _saveDot.Margin = new Thickness(UrbeTokens.Space.Xs, 0, 0, 0);
        _saveDot.RenderTransformOrigin = RelativePoint.Center;
        var titleRow = new DockPanel { LastChildFill = true, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(_saveDot, Dock.Right);
        titleRow.Children.Add(_saveDot);
        titleRow.Children.Add(_title);
        _titleButton = new Button
        {
            Content = titleRow, MinHeight = UrbeTokens.MinTouch, Padding = new Thickness(UrbeTokens.Space.Xs, 0),
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(UrbeTokens.Radius.M),
            HorizontalAlignment = HorizontalAlignment.Left, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center
        };
        _titleButton.Resources["ButtonBackgroundPointerOver"] = UrbeTheme.Brush(C.SurfaceOverlay, .6);
        _titleButton.Resources["ButtonBackgroundPressed"] = UrbeTheme.Brush(C.SurfaceOverlay);
        ToolTip.SetTip(_titleButton, "Renomear");
        UrbeMotion.Pressable(_titleButton);
        _titleButton.Click += (_, _) => { _visual.FinishEdit(); RenameRequested?.Invoke(); };

        _visualButton = Segment("Visual");
        _sourceButton = Segment("Fonte");
        _visualButton.Click += (_, _) => SetMode(EditorMode.Visual);
        _sourceButton.Click += (_, _) => SetMode(EditorMode.Fonte);
        Style(_visualButton, true);
        Style(_sourceButton, false);
        _segment = new Border
        {
            Background = UrbeTheme.Brush(C.SurfaceRaised),
            CornerRadius = new CornerRadius(UrbeTokens.Radius.M),
            Padding = new Thickness(2),
            Height = 40,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { _visualButton, _sourceButton } }
        };
        // v23: the overflow "Mais ações" after Visual/Fonte (the note's actions; a bottom sheet on a phone)
        var more = new UrbeIconButton(UrbeTheme.Icons.MoreUi, "Mais ações");
        more.Click += async (_, _) =>
        {
            _visual.FinishEdit();
            if (MoreRequested is not null && _note is not null) await MoreRequested(this);
        };
        var assistant = new UrbeIconButton(UrbeTheme.Icons.Assistant, "Assistente");
        assistant.Click += (_, _) =>
        {
            // the block being written is kept in the text, so the Assistente sees it and the selection
            _visual.FinishEdit();
            AssistantRequested?.Invoke();
        };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { PluginButtons, assistant, _segment, more } };
        var bar = new DockPanel { Margin = new Thickness(UrbeTokens.Space.Xxs, 0), LastChildFill = true };
        DockPanel.SetDock(back, Dock.Left);
        DockPanel.SetDock(right, Dock.Right);
        bar.Children.Add(back);
        bar.Children.Add(right);
        bar.Children.Add(_titleButton);
        return new Border
        {
            Height = HeaderHeight,
            ClipToBounds = true,
            Background = UrbeTheme.Brush(C.Background),
            BorderBrush = UrbeTheme.Brush(C.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = bar
        };
    }

    /// <summary>
    /// The order of the formatting bar (UX.md P4, P5): what is used most comes first, within reach of the thumb; the
    /// rest scrolls sideways. "|" is a separator. The buttons and what they do are the 1.8.4's (LegacyEditorTools).
    /// </summary>
    public static readonly string[] ToolbarOrder =
        ["bold", "italic", "list", "task", "|", "h1", "h2", "h3", "quote", "|", "noteLink", "link", "code", "box", "math", "rule"];

    // #mdToolbar (Markdown, in Fonte and in Visual, where it acts on the block of the caret) or .v21CodeTools (other text files, Fonte)
    private Control Toolbars()
    {
        var tools = LegacyEditorTools.Toolbar.ToDictionary(t => t.Id);
        foreach (var id in ToolbarOrder)
        {
            if (id == "|") { _markdownTools.Children.Add(new Border { Width = 1, Height = 24, Margin = new Thickness(UrbeTokens.Space.Xxs, 0), Background = UrbeTheme.Brush(C.BorderStrong), VerticalAlignment = VerticalAlignment.Center }); continue; }
            if (tools.TryGetValue(id, out var tool)) { _markdownTools.Children.Add(ToolButton(tool)); continue; }
            // math/editor.js ∑ (mdMath) and the UC-44 Caixa, a `:::tipo` container (not 1.8.4 #mdToolbar buttons)
            var (content, tip, run) = id == "math"
                ? ((Control)new TextBlock { Text = "∑", FontFamily = new FontFamily("Georgia, serif"), FontWeight = FontWeight.Normal, FontSize = 19 }, "Fórmula (LaTeX) — Ctrl+M", (Func<Task>)(() => StartFormulaAsync(false)))
                : (UrbeTheme.Icon(SlashMenu.Icons["box"], 22, C.TextSecondary, 1.8), "Caixa", InsertBoxAsync);
            var b = ToolButtonView(content, tip);
            b.Click += async (_, _) => await run();
            _markdownTools.Children.Add(b);
        }
        foreach (var (label, run) in new (string, Action)[]
                 {
                     ("⇥ Indentar", () => ApplyEdit(LegacyEditorTools.IndentSelection(Editor.Text ?? "", Editor.SelectionStart, Editor.SelectionEnd))),
                     ("{ } Formatar", FormatCurrent)
                 })
        {
            var b = new UrbeButton(label, ButtonKind.Ghost) { Focusable = false, MinHeight = UrbeTokens.MinTouch };
            b.Click += (_, _) => run();
            _codeTools.Children.Add(b);
        }
        var scroll = new ScrollViewer
        {
            Content = new Grid { Children = { _markdownTools, _codeTools } },
            // #mdToolbar{scrollbar-width:none}: it scrolls sideways with no bar
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        // "Concluir" (phone): leaves the block and lowers the keyboard — the way back to calm reading, one tap away
        var doneButton = new UrbeIconButton(UrbeTheme.Icons.Check, "Concluir edição", 22, C.Primary) { Focusable = false };
        doneButton.Click += (_, _) => FinishWriting();
        _done = new Border
        {
            Child = doneButton, IsVisible = false, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(UrbeTokens.Space.Xxs, 0),
            BorderBrush = UrbeTheme.Brush(C.Border), BorderThickness = new Thickness(1, 0, 0, 0)
        };
        var row = new DockPanel { LastChildFill = true, Height = 56 };
        DockPanel.SetDock(_done, Dock.Right);
        row.Children.Add(_done);
        row.Children.Add(scroll);
        _toolbarHost.Background = UrbeTheme.Brush(C.Surface);
        _toolbarHost.BorderBrush = UrbeTheme.Brush(C.Border);
        _toolbarHost.BorderThickness = new Thickness(0, 0, 0, 1);
        _toolbarHost.Child = row;
        AutomationProperties.SetName(_toolbarHost, "Formatação");
        return _toolbarHost;
    }

    /// <summary>Leaves the block being written (Visual) or the text (Fonte): the keyboard goes down, the note reads calm.</summary>
    public void FinishWriting()
    {
        _wiki.Hide();
        if (Mode == EditorMode.Visual) _visual.FinishEdit();
        // the focus leaves the text (the overlay takes it): the platform lowers the keyboard
        Focusable = true;
        Focus();
    }

    /// <summary>A button of the bar: 48 dp, the pressed feedback and a haptic tick; it never takes the focus (the caret and the keyboard stay).</summary>
    private static Button ToolButtonView(Control content, string tip)
    {
        var b = new Button
        {
            Content = content, Width = UrbeTokens.MinTouch, Height = UrbeTokens.MinTouch, Padding = new Thickness(0), CornerRadius = new CornerRadius(UrbeTokens.Radius.M), Focusable = false,
            Background = Brushes.Transparent, Foreground = UrbeTheme.Brush(C.TextSecondary), BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
        };
        b.Resources["ButtonBackgroundPointerOver"] = UrbeTheme.Brush(C.SurfaceOverlay, .7);
        b.Resources["ButtonBackgroundPressed"] = UrbeTheme.Brush(C.SurfaceOverlay);
        b.Resources["ButtonForegroundPointerOver"] = UrbeTheme.Brush(C.TextPrimary);
        b.Resources["ButtonForegroundPressed"] = UrbeTheme.Brush(C.TextPrimary);
        ToolTip.SetTip(b, tip);
        AutomationProperties.SetName(b, tip.Split(" — ")[0]);
        return UrbeMotion.Pressable(b, haptic: true);
    }

    private Control ToolButton(LegacyEditorTools.ToolbarButton tool)
    {
        (Control Off, Control On) content = tool.Id switch
        {
            "h1" or "h2" or "h3" => (Label(tool.Id.ToUpperInvariant(), FontWeight.SemiBold), Label(tool.Id.ToUpperInvariant(), FontWeight.SemiBold)),
            "bold" => (Label("B", FontWeight.Bold), Label("B", FontWeight.Bold)),
            "italic" => (Italic(), Italic()),
            _ => (UrbeTheme.Icon(SlashMenu.Icons[tool.Icon], 22, C.TextSecondary, 1.8), UrbeTheme.Icon(SlashMenu.Icons[tool.Icon], 22, C.Primary, 2))
        };
        var b = ToolButtonView(content.Off, tool.Title);
        b.Click += async (_, _) => await RunToolAsync(tool);
        _toolButtons[tool.Id] = (b, content.Off, content.On);
        return b;
        static TextBlock Label(string t, FontWeight w) => new() { Text = t, FontWeight = w, FontSize = 16, FontFamily = UrbeTheme.UiFont };
        static TextBlock Italic() => new() { Text = "I", FontFamily = new FontFamily("Georgia, serif"), FontStyle = FontStyle.Italic, FontSize = 18 };
    }

    private readonly Dictionary<string, (Button Button, Control Off, Control On)> _toolButtons = new(StringComparer.Ordinal);

    /// <summary>The toolbar buttons that show as on now (tests and the host).</summary>
    public IReadOnlySet<string> ActiveTools { get; private set; } = new HashSet<string>();

    // Fonte: the state of the buttons is the Markdown under the caret (DEC-0045, UC-37)
    private void UpdateActiveTools()
    {
        if (Mode != EditorMode.Fonte) return;
        var text = Editor.Text ?? "";
        ShowActiveTools(LegacyEditorTools.ActiveTools(text, Editor.SelectionStart, Editor.SelectionEnd));
    }

    // a lit button: a soft accent fill and its mark in the accent (never colour alone: the fill shows it too, P12)
    private void ShowActiveTools(IReadOnlySet<string> on)
    {
        ActiveTools = on;
        foreach (var (id, (button, off, lit)) in _toolButtons)
        {
            var active = on.Contains(id);
            button.Background = active ? UrbeTheme.Brush(C.Primary, .16) : Brushes.Transparent;
            button.Foreground = UrbeTheme.Brush(active ? C.Primary : C.TextSecondary);
            var want = active ? lit : off;
            if (!ReferenceEquals(button.Content, want)) button.Content = want;
            AutomationProperties.SetItemStatus(button, active ? "ativo" : "");
        }
    }

    private Border Footer()
    {
        UrbeTokens.Type.Caption.Apply(_status);
        _status.FontWeight = FontWeight.Normal;
        _status.Foreground = UrbeTheme.Brush(C.TextTertiary);
        _status.VerticalAlignment = VerticalAlignment.Center;
        AutomationProperties.SetName(_status, "Estatísticas da nota");
        return new Border { Height = 28, Padding = new Thickness(UrbeTokens.Space.M, 0), Child = _status };
    }

    /// <summary>The calm reading mode (UX.md P3, P11): on a phone, Visual with no block being written shows only the text.</summary>
    public bool Reading => Mode == EditorMode.Visual && !_visual.IsEditing && ToolbarAtBottom;

    private void UpdateChrome()
    {
        var fonte = Mode == EditorMode.Fonte;
        // v23AtualizarToolbar: #mdToolbar shows for Markdown in both modes; the code tools only in Fonte — but not
        // while just reading on a phone: the bar comes with the keyboard, right above it
        var show = (fonte || IsMarkdown) && !Reading;
        if (show != _toolbarHost.IsVisible)
        {
            _toolbarHost.IsVisible = show;
            if (show && ToolbarAtBottom && !UrbeTokens.Motion.Reduced) UrbeMotion.Enter(_toolbarHost, 0, 12, UrbeTokens.Motion.Fast);
        }
        _markdownTools.IsVisible = IsMarkdown;
        _codeTools.IsVisible = !IsMarkdown;
        // writing a block on a phone: the numbers of the footer give their line to the text
        if (_footer is not null) _footer.IsVisible = !(ToolbarAtBottom && Mode == EditorMode.Visual && _visual.IsEditing);
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
        _ = changed;
        UpdateSaveDot();
    }

    private string? _saveError;

    /// <summary>
    /// The save state the dot shows (UX.md P2): "saved" (in the vault, verified), "pending" (changed or being written:
    /// the same quiet dot, so a save never flickers), "error" (the last save failed; a toast offers to try again) or
    /// "preview" (no vault: nothing is written).
    /// </summary>
    public string SaveState =>
        !SaveOnClose || SaveRequested is null ? "preview" : _saveError is not null ? "error" : HasChanges || _saving ? "pending" : "saved";

    private string _shownSaveState = "";

    private void UpdateSaveDot()
    {
        var state = SaveState;
        if (state == _shownSaveState) return;
        var was = _shownSaveState;
        _shownSaveState = state;
        (string color, double opacity, string tip) = state switch
        {
            "saved" => (C.Success, .7, "Salvo e verificado no vault"),
            "pending" => (C.Warning, .9, "Alterações sendo salvas"),
            "error" => (C.Danger, 1.0, "Erro ao salvar: " + _saveError),
            _ => (C.TextTertiary, .8, "Prévia: não grava no vault")
        };
        _saveDot.Background = state == "preview" ? Brushes.Transparent : UrbeTheme.Brush(color, opacity);
        _saveDot.BorderBrush = UrbeTheme.Brush(color, opacity);
        _saveDot.BorderThickness = new Thickness(state == "preview" ? 1.5 : 0);
        ToolTip.SetTip(_saveDot, tip);
        AutomationProperties.SetName(_saveDot, tip);
        AutomationProperties.SetName(_titleButton, (_title.Text ?? "") + " — " + tip + ". Toque para renomear");
        // done: a small warm pulse of the dot (the pleasure of a saved note, without a word in the way)
        if (state == "saved" && was == "pending" && !UrbeTokens.Motion.Reduced)
        {
            _saveDot.Transitions = null;
            _saveDot.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("scale(1.8)");
            _saveDot.Transitions = [new TransformOperationsTransition { Property = RenderTransformProperty, Duration = UrbeTokens.Motion.Slow, Easing = UrbeTokens.Motion.Spring }];
            _saveDot.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("scale(1)");
        }
    }

    private void ScheduleAutosave()
    {
        if (!SaveOnClose || SaveRequested is null || _note is null || !HasChanges) return;
        var now = DateTime.UtcNow;
        if (!_autosave.IsEnabled) { _pendingSince = now; _autosave.Start(); return; }
        // still typing: push the save to the next pause, unless the edit has waited long enough
        if (now - _pendingSince < AutosaveMaxWait) { _autosave.Stop(); _autosave.Start(); }
    }

    /// <summary>Writes a pending edit now (the app is leaving the foreground): the pause of the autosave
    /// must not cost the last words.</summary>
    public async Task FlushAsync()
    {
        if (!_autosave.IsEnabled) return;
        await AutosaveTickAsync();
    }

    private async Task AutosaveTickAsync()
    {
        _autosave.Stop();
        if (!HasChanges) return;
        if (!_saving && await SaveAsync() && HasChanges) ScheduleAutosave();
        else if (_saving) ScheduleAutosave();
    }

    /// <summary>Save only via the native vault adapter, never silently to an in-memory preview.</summary>
    public Task<bool> SaveAsync() => Current is { } note ? SaveSnapshotAsync(note) : Task.FromResult(false);

    // one save at a time: a save running (the autosave) is waited for, then what changed since is saved too
    private async Task<bool> SaveSnapshotAsync(CityNote note)
    {
        bool waited = false;
        while (_saving && _saveRun is { } running) { await running; waited = true; }
        if (_saving) return false;
        var open = note.Path == _note?.Path;
        if (open && waited) { if (!HasChanges) return true; note = Current!; }
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
            UpdateSaveDot();
            return false;
        }
        _saving = true;
        UpdateSaveDot();
        try
        {
            await SaveRequested(note);
            // the note shown may be another one by now (leaving never waits): only its own save marks it saved
            if (note.Path == _note?.Path) { _savedContent = note.Content; _saveError = null; }
            _failed.Remove(note.Path);
            return true;
        }
        catch (Exception ex)
        {
            // workspace:saveError → 'Erro ao salvar': the dot turns red and a toast offers to try again (nothing is lost)
            if (note.Path == _note?.Path) _saveError = ex.Message;
            var name = System.IO.Path.GetFileNameWithoutExtension(note.Path);
            var message = $"Não foi possível salvar “{name}”. {ex.Message}";
            if (IsVisible) _toast.Show(message, TimeSpan.FromSeconds(8), "Tentar de novo", () => _ = RetrySavesAsync(), ToastKind.Warning);
            else SaveFailed?.Invoke(message);
            PlatformHaptics.Error();
            return false;
        }
        finally
        {
            _saving = false;
            UpdateSaveDot();
        }
    }

    /// <summary>A short notice over the text (a discreet toast that goes away by itself, UX.md P3).</summary>
    public void Notify(string message) => _toast.Show(message, TimeSpan.FromSeconds(3));

    /// <summary>The notice shown now, or null (tests).</summary>
    public string? NoticeText => _toast.Text;

    // ------------------------------------------------------------------ Fonte tools (app.js .mdBtn handlers)

    /// <summary>The plugins' buttons in the editor's top bar (native plugin API, "onde": "editor").</summary>
    public StackPanel PluginButtons { get; } = new() { Orientation = Orientation.Horizontal, Spacing = 2 };

    /// <summary>A plugin's text at the caret (Fonte) or at the end of the note (Visual); the note saves by itself.</summary>
    public void InsertText(string insert)
    {
        if (_note is null || insert.Length == 0) return;
        _visual.FinishEdit();
        var text = Editor.Text ?? "";
        var (a, b) = VisualSurface.Range(Editor);
        if (Mode == EditorMode.Visual)
        {
            a = b = text.Length;
            if (text.Length > 0 && !text.EndsWith('\n')) insert = "\n" + insert;
        }
        ApplyEdit(new LegacyEditorTools.TextEdit(text[..a] + insert + text[b..], a + insert.Length, a + insert.Length));
        if (Mode == EditorMode.Visual) _visual.SetText(Editor.Text ?? "");
    }

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
                Notify("Crie outra nota para poder vinculá-la.");   // informs only: the editor's toast (UC-69)
                return;
            }
            var pick = await Dialogs.ChooseAsync("Vincular nota", names.Select(n => new DialogItem(n, "file")).ToList());
            if (pick is not { } i) return;
            ApplyEdit(LegacyEditorTools.NoteLink(Editor.Text ?? text, a, b, names[i]));
            return;
        }
        // UC-55: a lit button turns its state off (the 1.8.4's insertAtCursor, ApplySource, only added marks: a lit B added
        // another pair), and Código puts the caret between the backticks; off buttons otherwise insert as the 1.8.4 did.
        var (s, e) = VisualSurface.Range(Editor);
        if (tool.Id is "bold" or "italic" or "code" && LegacyEditorTools.EnclosingMark(text, s, e, tool.Id) is not null)
        { ApplyEdit(LegacyVisualEdit.ToggleMark(text, s, e, tool.Id)); return; }
        if (tool.Id == "code") { ApplyEdit(LegacyVisualEdit.InlineCode(text, s, e)); return; }
        if (tool.Id is "h1" or "h2" or "h3" or "list" or "task" or "quote" && LegacyEditorTools.ActiveTools(text, s, e).Contains(tool.Id))
        { ApplyEdit(LegacyVisualEdit.ToggleBlock(text, s, tool.Md!)); return; }
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

    /// <summary>
    /// Caixa (UC-44): asks for the type and an optional title, then `:::tipo título` … `:::` goes around the selection
    /// (Fonte) or the selection of the block being edited (Visual); with none, an empty box with the caret inside.
    /// </summary>
    public async Task InsertBoxAsync()
    {
        if (_note is null || !IsMarkdown) return;
        if (Mode == EditorMode.Visual) { _visual.Toolbar("box"); return; }
        var text = Editor.Text ?? "";
        var (a, b) = VisualSurface.Range(Editor);
        if (await AskBoxAsync() is not { } box) { Editor.Focus(); return; }
        if (NoteContainers.Wrap(Editor.Text ?? text, a, b, box.Type, box.Title) is { } edit) ApplyEdit(edit);
    }

    private async Task<(string Type, string Title)?> AskBoxAsync()
    {
        if (Dialogs is null) return null;
        var type = await Dialogs.PromptAsync(new PromptOptions("Inserir caixa", "Tipo", Placeholder: "Ex.: ficha, aviso, dica",
            Hint: "Uma palavra, sem espaços. Vira :::tipo no texto.", Confirm: "Continuar",
            Validate: v => NoteContainers.IsType(v.Trim()) ? null : "Use uma palavra só (letras, números, - ou _)."));
        if (type is null) return null;
        var title = await Dialogs.PromptAsync(new PromptOptions("Inserir caixa", "Título (opcional)", Placeholder: "Ex.: Gandalf",
            Confirm: "Inserir", Required: false));
        if (title is null) return null;
        return (type.Trim(), title.Trim());
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
        var saving = LeaveCurrentAsync();
        if (await CreateLinked(name) is { } made) Go(made);
        await saving;
    }

    // ------------------------------------------------------------------ small controls

    // a half of the Visual/Fonte switch: 36 dp tall inside the 40 dp track (the header row is the touch area, 48 dp+)
    private static Button Segment(string text)
    {
        var b = new Button
        {
            Content = text,
            Height = 36,
            MinWidth = 60,
            Padding = new Thickness(UrbeTokens.Space.S, 0),
            CornerRadius = new CornerRadius(UrbeTokens.Radius.M - 2),
            FontFamily = UrbeTheme.UiFont,
            FontWeight = FontWeight.SemiBold,
            FontSize = UrbeTokens.Type.Label.Size,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(b, text);
        ToolTip.SetTip(b, text == "Visual" ? "Ver a nota formatada" : "Ver o Markdown");
        return UrbeMotion.Pressable(b, haptic: true);
    }

    private static void Style(Button b, bool active)
    {
        b.Background = active ? UrbeTheme.Brush(C.SurfaceOverlay) : Brushes.Transparent;
        b.Foreground = UrbeTheme.Brush(active ? C.TextPrimary : C.TextTertiary);
        b.Resources["ButtonBackgroundPointerOver"] = active ? UrbeTheme.Brush(C.SurfaceOverlay) : UrbeTheme.Brush(C.SurfaceOverlay, .5);
        b.Resources["ButtonBackgroundPressed"] = UrbeTheme.Brush(C.SurfaceOverlay);
        b.Resources["ButtonForegroundPointerOver"] = b.Foreground;
        b.Resources["ButtonForegroundPressed"] = UrbeTheme.Brush(C.TextPrimary);
        AutomationProperties.SetItemStatus(b, active ? "selecionado" : "");
    }
}
