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
    private readonly Border _toolbarHost = new();
    private readonly StackPanel _markdownTools = new() { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(10, 0) };
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
    public EditorMode Mode { get; private set; } = EditorMode.Visual;
    /// <summary>"Abrir notas no modo" (Personalização → editor.modoInicial): Visual unless "texto".</summary>
    public bool StartInVisual { get; set; } = true;
    /// <summary>The notes of the open vault (for [[ suggestions and wikilink targets).</summary>
    public Func<IEnumerable<CityNote>>? Notes { get; set; }
    /// <summary>UD.prompt / UD.choose of the host.</summary>
    public UrbeDialogs? Dialogs { get; set; }
    public VisualSurface Surface => _visual;

    public EditorOverlay()
    {
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        RowDefinitions = new RowDefinitions("56,Auto,*,48");
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
        Editor.PropertyChanged += (_, e) => { if (e.Property == TextBox.CaretIndexProperty) UpdateWiki(); };
        Editor.AddHandler(KeyDownEvent, (object? _, KeyEventArgs e) => { if (_wiki.HandleKey(e.Key)) e.Handled = true; }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _wiki.Chosen += CommitWiki;

        _visual.TextEdited += md =>
        {
            _fromVisual = true;
            if (!string.Equals(Editor.Text, md, StringComparison.Ordinal)) Editor.Text = md;
            _fromVisual = false;
        };
        _visual.WikiLinkTapped += name => _ = OpenLinkedAsync(name);
        _visual.WikiCandidates = Candidates;
        _visual.AskUrl = AskUrlAsync;

        AddRow(TopBar(), 0);
        AddRow(Toolbars(), 1);
        _sourceColumn = new StackPanel { Children = { Editor, _wiki }, HorizontalAlignment = HorizontalAlignment.Center };
        _sourceScroll = Column(_sourceColumn);
        _visualScroll = Column(_visual);
        var body = new Grid { Children = { _sourceScroll, _visualScroll } };
        AddRow(body, 2);
        AddRow(Footer(), 3);
        _autosave.Tick += async (_, _) => await AutosaveTickAsync();
        ApplyOptions(null, null);
        ShowMode(EditorMode.Visual);
    }

    public TextBox Editor { get; }
    public event EventHandler? Closed;

    // ------------------------------------------------------------------ open / close

    public void Open(CityNote note)
    {
        _opening = true;
        _autosave.Stop();
        _note = note;
        _savedContent = note.Content;
        _title.Text = System.IO.Path.GetFileName(note.Path);
        Editor.Text = note.Content;
        _opening = false;
        RebuildIndex();
        var canVisual = CanVisual(note.Path);
        _segment.IsVisible = canVisual;
        ShowMode(canVisual && StartInVisual ? EditorMode.Visual : EditorMode.Fonte);
        UpdateChrome();
        UpdateStatus(changed: false);
        IsVisible = true;
        // Focus after the overlay is laid out; on Android this brings up the soft keyboard (Fonte; Visual edits on tap).
        if (Mode == EditorMode.Fonte) Dispatcher.UIThread.Post(() => Editor.Focus(), DispatcherPriority.Loaded);
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

    public void SetMode(EditorMode mode) => ShowMode(mode == EditorMode.Visual && (_note is null || CanVisual(_note.Path)) ? EditorMode.Visual : EditorMode.Fonte);

    private void ShowMode(EditorMode mode)
    {
        if (Mode == EditorMode.Visual && mode != EditorMode.Visual) _visual.FinishEdit();   // serialise what was edited
        Mode = mode;
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

    private void AddRow(Control control, int row)
    {
        SetRow(control, row);
        Children.Add(control);
    }

    private ScrollViewer Column(Control content)
    {
        var host = new Border { Child = content, Padding = new Thickness(20, 28, 20, 48), HorizontalAlignment = HorizontalAlignment.Stretch };
        return new ScrollViewer
        {
            Content = host,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
    }

    private Control TopBar()
    {
        var back = IconButton(UrbeTheme.Icons.Back, "Voltar para a cidade");
        back.Click += async (_, _) =>
        {
            _visual.FinishEdit();
            _autosave.Stop();
            if (SaveOnClose && HasChanges && !await SaveAsync()) return;
            IsVisible = false;
            Closed?.Invoke(this, EventArgs.Empty);
        };
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
        var save = new Button
        {
            Content = "Salvar", MinWidth = 64, Height = 36, Margin = new Thickness(4, 0),
            Foreground = UrbeTheme.Brush(UrbeTheme.AccentInk),
            Background = UrbeTheme.Brush(UrbeTheme.Accent),
            CornerRadius = new CornerRadius(9)
        };
        save.Click += async (_, _) => { _visual.FinishEdit(); await SaveAsync(); };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { save, _segment } };
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

    // #mdToolbar (Fonte, Markdown) or .v21CodeTools (other text files); hidden in Visual, where the bubble and "/" menu act
    private Control Toolbars()
    {
        foreach (var button in LegacyEditorTools.Toolbar)
        {
            _markdownTools.Children.Add(ToolButton(button));
            if (LegacyEditorTools.SeparatorAfter.Contains(button.Id))
                _markdownTools.Children.Add(new Border { Width = 1, Height = 20, Margin = new Thickness(6, 0), Background = UrbeTheme.Brush(UrbeTheme.Line2) });
        }
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
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
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
        return b;
        static TextBlock Label(string t) => new() { Text = t, FontWeight = FontWeight.Bold, FontSize = 14 };
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
        _toolbarHost.IsVisible = fonte;
        _markdownTools.IsVisible = IsMarkdown;
        _codeTools.IsVisible = !IsMarkdown;
    }

    // ------------------------------------------------------------------ text changes, status, autosave

    private void OnTextChanged()
    {
        if (_opening) return;
        UpdateStatus(changed: true);
        if (!_fromVisual && Mode == EditorMode.Fonte) UpdateWiki();
        ScheduleAutosave();
    }

    private void UpdateStatus(bool changed)
    {
        _status.Text = LegacyEditorTools.Stats(Editor.Text ?? "", _note is null ? "md" : Kind(_note.Path));
        _saveState.Text = _saving ? "Salvando…" :
            SaveOnClose ? (HasChanges ? "Alterado · não salvo" : "Sem alterações") :
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
        if (_saving || Current is not { } note) return false;
        if (SaveRequested is null)
        {
            _saveState.Text = "Sem armazenamento conectado";
            return false;
        }
        _saving = true;
        _saveState.Text = "Salvando…";
        try
        {
            await SaveRequested(note);
            _savedContent = note.Content;
            _saveState.Text = "Salvo e verificado no vault";
            return true;
        }
        catch (Exception ex)
        {
            _saveState.Text = "Falha ao salvar: " + ex.Message;
            return false;
        }
        finally { _saving = false; }
    }

    private void Notify(string message)
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
    private async Task OpenLinkedAsync(string name)
    {
        var all = Notes?.Invoke().ToList() ?? [];
        var exact = all.FirstOrDefault(n => string.Equals(n.Path, name, StringComparison.Ordinal) || string.Equals(n.Path, name + ".md", StringComparison.Ordinal));
        var matches = all.Where(n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
        var target = exact ?? (matches.Count == 1 ? matches[0] : null);
        if (target is null) { Notify(matches.Count > 1 ? "Link ambíguo: use o caminho da nota." : "A nota vinculada não existe."); return; }
        _visual.FinishEdit();
        _autosave.Stop();
        if (SaveOnClose && HasChanges && !await SaveAsync()) return;
        Open(target.Path == _note?.Path ? Current! : target);
    }

    // ------------------------------------------------------------------ small controls

    private static Button IconButton(string icon, string tip)
    {
        var b = new Button
        {
            Content = UrbeTheme.Icon(icon, 20),
            Width = 40,
            Height = 40,
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
