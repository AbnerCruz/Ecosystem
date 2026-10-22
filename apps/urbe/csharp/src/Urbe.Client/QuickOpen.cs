using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>One line of a search list: what to draw and what choosing it does.</summary>
public sealed record SearchRow(Control Content, Action Pick);

/// <summary>
/// The overlay shared by "Buscar notas" (#urbeQuickOpen) and "Comandos" (#urbeCommandPalette): a backdrop, a
/// panel that is a bottom sheet under 600 px and a card with a top margin above, the input, the list and
/// the keys (↑ ↓ move, Enter picks, Esc closes). The rules (ranking, filtering) live in Urbe.Core.
/// </summary>
public abstract class SearchPanel : Grid
{
    private const double Narrow = 600;
    private readonly Border _card = new();
    private readonly StackPanel _list = new() { Margin = new Thickness(6) };
    private readonly ScrollViewer _scroll;
    private readonly List<(Border Box, SearchRow Row)> _rows = [];
    private int _active;

    protected SearchPanel(string placeholder, Control? leading, Control? footer)
    {
        IsVisible = false;
        Background = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)); // rgba(0,0,0,.55)
        Input = new TextBox
        {
            PlaceholderText = placeholder, FontFamily = UrbeTheme.UiFont, FontSize = 17, FontWeight = FontWeight.Medium,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0, 18),
            VerticalContentAlignment = VerticalAlignment.Center, Foreground = UrbeTheme.Brush(UrbeTheme.Text), MinHeight = 56
        };
        var field = new DockPanel { Margin = new Thickness(18, 0) };
        if (leading is not null)
        {
            leading.VerticalAlignment = VerticalAlignment.Center;
            leading.Margin = new Thickness(0, 0, 12, 0);
            DockPanel.SetDock(leading, Dock.Left);
            field.Children.Add(leading);
        }
        field.Children.Add(Input);
        _scroll = new ScrollViewer { Content = _list, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        var panel = new DockPanel();
        var top = new Border { Child = field, BorderBrush = UrbeTheme.Brush(UrbeTheme.Line), BorderThickness = new Thickness(0, 0, 0, 1) };
        DockPanel.SetDock(top, Dock.Top);
        panel.Children.Add(top);
        if (footer is not null)
        {
            DockPanel.SetDock(footer, Dock.Bottom);
            panel.Children.Add(footer);
            Footer = footer;
        }
        panel.Children.Add(_scroll);
        _card.Child = panel;
        _card.Background = UrbeTheme.Brush(UrbeTheme.Surface);
        _card.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        _card.BoxShadow = BoxShadows.Parse("0 24 80 0 #73000000");
        _card.ClipToBounds = true;
        Children.Add(_card);

        PointerPressed += (_, e) => { if (e.Source == this) Close(); };
        Input.TextChanged += (_, _) => { if (IsVisible) { _active = 0; Render(Input.Text ?? ""); } };
        Input.AddHandler(KeyDownEvent, OnInputKey, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
        Focusable = true;
    }

    public TextBox Input { get; }
    protected Control? Footer { get; }
    public bool IsOpen => IsVisible;
    public int ActiveIndex => _active;
    public int RowCount => _rows.Count;
    /// <summary>Fired after the panel closes.</summary>
    public event Action? Closed;

    /// <summary>Draws the rows for the text typed so far (calls <see cref="Show"/>).</summary>
    protected abstract void Render(string typed);

    public void Open()
    {
        Input.Text = "";
        _active = 0;
        IsVisible = true;
        Render("");
        Layout(Bounds.Width, Bounds.Height);
        Dispatcher.UIThread.Post(() => Input.Focus(), DispatcherPriority.Loaded);
    }

    public void Close()
    {
        if (!IsVisible) return;
        IsVisible = false;
        Closed?.Invoke();
    }

    /// <summary>Replace the list: an optional group heading, the rows or the empty message.</summary>
    protected void Show(IReadOnlyList<SearchRow> rows, string? group, string emptyText)
    {
        _list.Children.Clear();
        _rows.Clear();
        if (rows.Count == 0)
        {
            _list.Children.Add(new TextBlock
            {
                Text = emptyText, Margin = new Thickness(24), HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap, FontFamily = UrbeTheme.UiFont, FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text3)
            });
            return;
        }
        if (group is not null)
            _list.Children.Add(new TextBlock
            {
                Text = group.ToUpperInvariant(), Margin = new Thickness(12, 10, 12, 4), FontFamily = UrbeTheme.UiFont, FontSize = 12,
                FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text3)
            });
        _active = LegacyQuickOpen.Clamp(_active, rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var box = new Border { Child = row.Content, CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 12), Cursor = new Cursor(StandardCursorType.Hand) };
            var index = i;
            box.PointerPressed += (_, e) => { e.Handled = true; _active = index; row.Pick(); };
            _rows.Add((box, row));
            _list.Children.Add(box);
        }
        Paint();
    }

    private void Paint()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            _rows[i].Box.Background = i == _active ? UrbeTheme.Brush(UrbeTheme.Accent, .14) : Brushes.Transparent;
            Avalonia.Automation.AutomationProperties.SetName(_rows[i].Box, i == _active ? "ativo" : "");
        }
        if (_active < _rows.Count) _rows[_active].Box.BringIntoView();
    }

    private void OnInputKey(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape: e.Handled = true; Close(); break;
            case Key.Down: e.Handled = true; _active = LegacyQuickOpen.Move(_active, 1, _rows.Count); Paint(); break;
            case Key.Up: e.Handled = true; _active = LegacyQuickOpen.Move(_active, -1, _rows.Count); Paint(); break;
            case Key.Enter when _rows.Count > _active: e.Handled = true; _rows[_active].Row.Pick(); break;
        }
    }

    /// <summary>Choose the active row (what Enter does).</summary>
    public void PickActive() { if (_rows.Count > _active) _rows[_active].Row.Pick(); }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Layout(e.NewSize.Width, e.NewSize.Height);
    }

    private void Layout(double width, double height)
    {
        var narrow = width < Narrow;
        _card.HorizontalAlignment = narrow ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
        _card.VerticalAlignment = narrow ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        _card.Width = narrow ? double.NaN : Math.Min(680, Math.Max(0, width - 24));
        _card.Margin = narrow ? new Thickness(0) : new Thickness(0, Math.Max(24, height * .12), 0, 0);
        _card.MaxHeight = narrow ? Math.Max(200, height * .72) : Math.Max(200, Math.Min(620, height - 40));
        _card.CornerRadius = narrow ? new CornerRadius(20, 20, 0, 0) : new CornerRadius(20);
        _card.BorderThickness = narrow ? new Thickness(0, 1, 0, 0) : new Thickness(1);
        if (Footer is not null) Footer.IsVisible = !narrow;
    }
}

/// <summary>src/ui/quick-open.js: "Buscar notas" — recent notes, ranked results with an excerpt, and "Criar nota".</summary>
public sealed class QuickOpenPanel : SearchPanel
{
    private readonly Func<IReadOnlyList<CityNote>> _notes;
    private readonly List<string> _recent = [];

    public QuickOpenPanel(Func<IReadOnlyList<CityNote>> notes)
        : base("Buscar ou criar nota…", UrbeTheme.Icon(UrbeTheme.Icons.Search, 20, UrbeTheme.Text3), FooterBar())
    {
        _notes = notes;
    }

    /// <summary>The note chosen (vault-relative path).</summary>
    public event Action<string>? NoteChosen;
    /// <summary>"Criar nota “x”": the path (<c>x.md</c>, in the root) to create or open.</summary>
    public event Action<string>? CreateChosen;

    /// <summary>Explorer.touchRecent: most recent first, 30 at most.</summary>
    public void TouchRecent(string path)
    {
        _recent.Remove(path);
        _recent.Insert(0, path);
        if (_recent.Count > 30) _recent.RemoveRange(30, _recent.Count - 30);
    }

    protected override void Render(string typed)
    {
        var store = new DocumentStore();
        var docs = _notes().Select(n =>
        {
            var d = store.Upsert(new DocumentInput { Path = n.Path, Content = n.Content });
            return new LegacySearchDoc(n.Path, d.Title, n.Path, n.Content, d.Modified);
        }).ToList();
        var view = LegacyQuickOpen.View(docs, _recent, typed);
        var rows = new List<SearchRow>();
        foreach (var r in view.Rows)
        {
            if (r.Create is { } create)
                rows.Add(new SearchRow(Item(UrbeTheme.Icons.Plus, "Criar nota “" + create + "”", "Na raiz · uma nota nova no vault", null, true),
                    () => Choose(() => CreateChosen?.Invoke(LegacyQuickOpen.CreatePath(create)))));
            else
            {
                var doc = r.Doc!;
                rows.Add(new SearchRow(Item(UrbeTheme.Icons.File, doc.Title, r.Folder.Length > 0 ? r.Folder : null, r.Snippet, false),
                    () => Choose(() => { TouchRecent(doc.Path); NoteChosen?.Invoke(doc.Path); })));
            }
        }
        Show(rows, view.RecentGroup ? "Recentes" : null, "Nenhuma nota ainda. Digite um nome para criar a primeira.");
    }

    private void Choose(Action then)
    {
        Close();
        then();
    }

    private static Control Item(string icon, string title, string? detail, LegacySearchSnippet? snippet, bool create)
    {
        var box = new Border
        {
            Width = 32, Height = 32, CornerRadius = new CornerRadius(8), VerticalAlignment = VerticalAlignment.Top,
            Background = create ? UrbeTheme.Brush(UrbeTheme.Accent, .14) : UrbeTheme.Brush(UrbeTheme.Surface2),
            Child = UrbeTheme.Icon(icon, 18, create ? UrbeTheme.Accent : UrbeTheme.Text3)
        };
        var text = new StackPanel { Spacing = 3, Margin = new Thickness(12, 5, 0, 0) };
        text.Children.Add(new TextBlock
        {
            Text = title, FontFamily = UrbeTheme.UiFont, FontSize = 15, FontWeight = FontWeight.SemiBold,
            Foreground = UrbeTheme.Brush(UrbeTheme.Text), TextTrimming = TextTrimming.CharacterEllipsis
        });
        if (snippet is not null)
        {
            var t = new TextBlock { FontFamily = UrbeTheme.UiFont, FontSize = 13, LineHeight = 18, TextWrapping = TextWrapping.Wrap, MaxLines = 2, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) };
            t.Inlines!.Add(new Run(snippet.Before));
            t.Inlines.Add(new Run(snippet.Match) { Background = UrbeTheme.Brush(UrbeTheme.Accent, .22), Foreground = UrbeTheme.Brush(UrbeTheme.Text) });
            t.Inlines.Add(new Run(snippet.After));
            text.Children.Add(t);
        }
        else if (detail is not null)
            text.Children.Add(new TextBlock { Text = detail, FontFamily = UrbeTheme.UiFont, FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextTrimming = TextTrimming.CharacterEllipsis });
        var row = new DockPanel();
        DockPanel.SetDock(box, Dock.Left);
        row.Children.Add(box);
        row.Children.Add(text);
        return row;
    }

    private static Control FooterBar()
    {
        TextBlock Hint(string key, string what) => new()
        {
            Text = key + "  " + what, FontFamily = UrbeTheme.UiFont, FontSize = 12, FontWeight = FontWeight.Medium, Foreground = UrbeTheme.Brush(UrbeTheme.Text3)
        };
        return new Border
        {
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(18, 10),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Children = { Hint("↑ ↓", "navegar"), Hint("Enter", "abrir"), Hint("Esc", "fechar") } }
        };
    }
}

/// <summary>A command of the native app: its 1.8.4 identity (id, title, category) and what it runs.</summary>
public sealed record NativeCommand(LegacyCommand Info, Action Run);

/// <summary>src/ui/command-palette.js: "Digite um comando…" — the commands of the app that match, best first.</summary>
public sealed class CommandPalettePanel : SearchPanel
{
    private readonly Func<IReadOnlyList<NativeCommand>> _commands;

    public CommandPalettePanel(Func<IReadOnlyList<NativeCommand>> commands) : base("Digite um comando…", null, null)
    {
        _commands = commands;
    }

    protected override void Render(string typed)
    {
        var all = _commands();
        var rows = LegacyCommandPalette.Filter(all.Select(c => c.Info), typed).Select(info =>
        {
            var run = all.Last(c => c.Info == info).Run;
            var row = new DockPanel();
            var category = new TextBlock
            {
                Text = info.Category, FontFamily = UrbeTheme.UiFont, FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
                Foreground = UrbeTheme.Brush(UrbeTheme.Text3), Margin = new Thickness(16, 0, 0, 0)
            };
            DockPanel.SetDock(category, Dock.Right);
            row.Children.Add(category);
            row.Children.Add(new TextBlock
            {
                Text = info.Title, FontFamily = UrbeTheme.UiFont, FontSize = 15, FontWeight = FontWeight.Medium,
                Foreground = UrbeTheme.Brush(UrbeTheme.Text), TextTrimming = TextTrimming.CharacterEllipsis
            });
            return new SearchRow(row, () => { Close(); run(); });
        }).ToList();
        Show(rows, null, "Nenhum comando encontrado");
    }
}

/// <summary>
/// The 1.8.4 search wiring on <see cref="MainView"/>: the toolbar "Buscar" button, Ctrl/Cmd+P (notas) and
/// Ctrl/Cmd+K (comandos) through keymap.js, and the commands the native app already has.
/// Choosing a note opens it; "Criar nota" makes a new note in the root.
/// </summary>
public sealed class SearchFeature
{
    private readonly MainView _view;
    private readonly List<NativeCommand> _commands = [];
    private TopLevel? _hooked;

    public SearchFeature(MainView view, Func<IReadOnlyList<CityNote>> notes)
    {
        _view = view;
        QuickOpen = new QuickOpenPanel(notes);
        Palette = new CommandPalettePanel(() => _commands);
        foreach (var panel in new SearchPanel[] { QuickOpen, Palette })
        {
            Grid.SetRowSpan(panel, 2);
            Grid.SetColumnSpan(panel, 2);
            view.Children.Insert(Math.Max(0, view.Children.IndexOf(view.Dialogs)), panel);
        }
        QuickOpen.NoteChosen += OpenNote;
        QuickOpen.CreateChosen += path => _ = CreateOrOpenAsync(path);

        Register("ui.quickOpen.open", "Buscar notas", "Navegação", OpenQuickOpen);
        Register("ui.commandPalette.open", "Abrir comandos", "Interface", OpenPalette);
        Register("ui.explorer.open", "Abrir notas", "Navegação", view.ShowNotes);
        Register("document.create", "Nova nota", "Documento", () => _ = view.CreateNoteAsync());
        Register("ui.customize", "Personalização", "Aplicativo", () => view.Customize.Open());
        Register("pages.home", "Páginas", "Páginas", view.ShowPages);
        Register("workspace.save", "Salvar workspace", "Workspace", () => _ = view.Editor.SaveAsync(), () => view.Editor.IsVisible);
        Register("ui.find.open", "Localizar e substituir", "Editor", view.Editor.FindBar.Open, () => view.Editor.IsVisible);
        Register("editor.split.open", "Abrir em painel dividido", "Editor", () => _ = view.ChooseSplitAsync());
        Register("editor.split.close", "Fechar painel dividido", "Editor", view.CloseSplit, () => view.SplitState.Enabled);
        Register("editor.split.swap", "Trocar painéis", "Editor", view.SwapSplit, () => view.SplitState.Enabled);
        // the rest of the 1.8.4's commands (and so their shortcuts: Mod+W, Alt+←/→)
        Register("editor.back", "Voltar", "Navegação", () => _ = view.Editor.BackAsync(), () => view.Editor.IsVisible);
        Register("editor.forward", "Avançar", "Navegação", () => _ = view.Editor.ForwardAsync(), () => view.Editor.IsVisible && view.Editor.CanForward);
        Register("editor.closeTab", "Fechar aba", "Editor", () => { if (view.Editor.Current is { } n) _ = view.Editor.CloseTabAsync(n.Path); }, () => view.Editor.IsVisible);
        Register("editor.undo", "Desfazer", "Editor", view.Editor.Undo, () => view.Editor.IsVisible);
        Register("editor.redo", "Refazer", "Editor", view.Editor.Redo, () => view.Editor.IsVisible);
        Register("editor.contextPanel", "Outline e backlinks", "Editor", view.ShowEditorContext, () => view.Editor.IsVisible);
        Register("editor.session.restore", "Restaurar sessão", "Editor", () => { if (!view.RestoreSession()) view.Notify("Nenhuma sessão para restaurar."); });
        Register("history.restore", "Restaurar versão", "Editor", () => _ = view.ShowVersionsAsync(), () => view.Editor.IsVisible);
        Register("math.insert", "Inserir fórmula", "Editor", () => _ = view.Editor.StartFormulaAsync(false), () => view.Editor.IsVisible);
        Register("trash.restore", "Restaurar da lixeira", "Notas", () => { view.ShowNotes(); view.ShowTrashView(); });
        Register("customize.reset", "Restaurar aparência padrão", "Personalização", () => _ = view.Customization.ResetAsync());
        Register("customize.edit-file", "Abrir tema.json", "Personalização", () => _ = view.EditThemeFileAsync());
        Register("ui.plugins", "Plugins", "Personalização", () => view.Customize.Open("plugins"));
        Register("plugins.reload", "Recarregar plugins", "Personalização", view.SyncPlugins);
        Register("pages.open", "Abrir página no estúdio", "Páginas", () => _ = view.ChoosePageForStudioAsync());
        Register("tutorial.open", "Tutorial", "Ajuda", () => _ = view.ShowTutorialMenuAsync());
        Register("tutorial.restore", "Restaurar o Tutorial", "Ajuda", () => _ = view.RestoreTutorialAsync(true));
        Register("editor.find", "Localizar no documento", "Editor", view.Editor.FindBar.Open, () => view.Editor.IsVisible);
        Register("editor.replace", "Substituir no documento", "Editor", view.Editor.FindBar.Open, () => view.Editor.IsVisible);
        Register("pages.new", "Nova página", "Páginas", () => _ = view.NewPageAsync());
        Register("workspace.backups", "Backups do vault", "Workspace", () => _ = view.ShowBackupsAsync());
        Register("workspace.cleanOrphans", "Limpar referências órfãs", "Workspace", () => _ = view.CleanOrphansAsync());
        Register("workspace.diagnostics", "Diagnóstico do workspace", "Workspace", () => _ = view.ShowDiagnosticsAsync());
        Register("tutorial.menu", "Tutorial: abrir ou restaurar", "Ajuda", () => _ = view.ShowTutorialMenuAsync());
        Register("ui.settings", "Configurações", "Aplicativo", () => _ = view.ShowSettingsAsync());
        Register("ui.assistant", "Assistente", "Aplicativo", view.ToggleAssistant);
        // UC-61: the screens and actions that came after the 1.8.4 (DEC-0053, DEC-0052, UC-80, UC-58)
        Register("ui.workspace", "Área de trabalho", "Navegação", view.ShowWorkspace);
        Register("ui.sidebar.toggle", "Mostrar ou recolher a barra lateral", "Navegação", view.ToggleSidebar);
        Register("ui.constellation", "Constelação", "Navegação", () => _ = view.ShowConstellationAsync(), () => view.Documents is not null);
        Register("editor.connections", "Conexões da nota", "Editor", view.ShowEditorConnections, () => view.Editor.IsVisible);
        Register("workspace.newProject", "Novo projeto", "Workspace", () => _ = view.CreateProjectAsync());
        Register("document.rename", "Renomear nota", "Documento", () => { if (view.Editor.Current is { } n) _ = view.RenameNoteAsync(n.Path); }, () => view.Editor.IsVisible);
        Register("ui.firstRun", "Configuração inicial", "Aplicativo", view.ShowFirstRun);

        // document.addEventListener('keydown'): on the top level, so it works with nothing focused too
        void Hook()
        {
            if (TopLevel.GetTopLevel(view) is { } top && !ReferenceEquals(top, _hooked))
            {
                _hooked = top;
                top.AddHandler(InputElement.KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            }
        }
        view.AttachedToVisualTree += (_, _) => Hook();
        Hook();
    }

    /// <summary>UC-61: the person's shortcuts (Personalização/atalhos.json), over the defaults.</summary>
    public Func<IReadOnlyDictionary<string, string>?>? UserShortcuts { get; set; }

    /// <summary>The title of a command, or null.</summary>
    public string? TitleOf(string id) => _commands.LastOrDefault(c => c.Info.Id == id)?.Info.Title;

    public QuickOpenPanel QuickOpen { get; }
    public CommandPalettePanel Palette { get; }

    /// <summary>core.commands.register(id, …) for a plugin (category "Plugin · name"); returns what removes it.</summary>
    public Action AddCommand(string id, string title, string category, Action run)
    {
        var command = new NativeCommand(new LegacyCommand(id, title, category, null), run);
        _commands.Add(command);
        return () => _commands.Remove(command);
    }

    /// <summary>core.commands.has(id).</summary>
    public bool Has(string id) => _commands.Any(c => c.Info.Id == id);

    private void Register(string id, string title, string category, Action run, Func<bool>? enabled = null) =>
        _commands.Add(new NativeCommand(new LegacyCommand(id, title, category, enabled), run));

    /// <summary>core.commands.execute(id): false when the command is disabled.</summary>
    public bool Execute(string id)
    {
        var command = _commands.Last(c => c.Info.Id == id);
        if (!command.Info.IsEnabled) return false;
        command.Run();
        return true;
    }

    public void OpenQuickOpen() { Palette.Close(); QuickOpen.Open(); }

    public void OpenPalette() { QuickOpen.Close(); Palette.Open(); }

    private void OpenNote(string path) => _view.OpenNoteByPath(path);

    private async Task CreateOrOpenAsync(string path)
    {
        if (_view.Documents?.Get(path) is not null) { OpenNote(path); return; }
        await _view.CreateNoteAsync(null, Path.GetFileNameWithoutExtension(path));
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (KeyName(e) is not { } key) return;
        var input = new LegacyKeyInput(key, e.KeyModifiers.HasFlag(KeyModifiers.Control), e.KeyModifiers.HasFlag(KeyModifiers.Meta),
            e.KeyModifiers.HasFlag(KeyModifiers.Alt), e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        var editable = e.Source is TextBox or Avalonia.Controls.Presenters.TextPresenter;
        var resolved = LegacyKeymap.Resolve(input, editable, Has, UserShortcuts?.Invoke());
        if (resolved.CommandId is null) return;
        e.Handled = resolved.PreventDefault;
        Execute(resolved.CommandId);
    }

    /// <summary>KeyboardEvent.key for the keys the keymap uses.</summary>
    internal static string? KeyName(KeyEventArgs e) => e.Key switch
    {
        >= Key.A and <= Key.Z => ((char)('a' + (e.Key - Key.A))).ToString(),
        >= Key.D0 and <= Key.D9 => ((char)('0' + (e.Key - Key.D0))).ToString(),
        Key.Left => "ArrowLeft",
        Key.Right => "ArrowRight",
        Key.Up => "ArrowUp",
        Key.Down => "ArrowDown",
        Key.OemBackslash or Key.OemPipe => "\\",
        Key.Enter => "Enter",
        Key.Escape => "Escape",
        _ => e.KeySymbol is { Length: 1 } s ? s : null
    };
}
