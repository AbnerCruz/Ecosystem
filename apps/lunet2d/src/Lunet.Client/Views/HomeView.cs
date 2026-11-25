using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Lunet.Client.Ui;
using Lunet.Core;

namespace Lunet.Client.Views;

/// <summary>
/// Level 1 of the shell: your games, and the action of making a new one. One place, no repeated destinations
/// (the old Home showed Projetos/Studios/Explorar/Aprender both as cards and in the bar).
/// </summary>
public sealed class HomeView : UserControl
{
    private readonly LunetShell _shell;
    private readonly WrapPanel _grid = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock _count = LunetUi.Secondary("");
    private readonly StackPanel _trashRow = new() { Margin = new Thickness(0, LunetTokens.Space.S, 0, 0) };
    // Sheets and the undo bar float over the Home; the Home stays in place under them (no navigation).
    private readonly Panel _overlay = new();
    private readonly Border _snackbar = new() { IsVisible = false };
    private readonly DispatcherTimer _snackbarTimer = new() { Interval = SnackbarDuration };

    /// <summary>How long "Desfazer" stays (Material: long enough to read and reach, 4–10 s).</summary>
    public static readonly TimeSpan SnackbarDuration = TimeSpan.FromSeconds(8);

    public HomeView(LunetShell shell)
    {
        _shell = shell;
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
        _snackbarTimer.Tick += (_, _) => HideSnackbar();
        Build();
    }

    /// <summary>Lays the Home out (again when the head sets <see cref="LunetShell.OpenRoute"/>).</summary>
    public void Build()
    {
        var shell = _shell;
        if (_grid.Parent is Panel old) old.Children.Remove(_grid);
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, LunetTokens.Space.L) };
        var create = LunetUi.Button("Novo jogo", ButtonKind.Primary, LunetIcons.Plus, ShowCreate);
        create.Name = "NewProject";
        DockPanel.SetDock(create, Avalonia.Controls.Dock.Right);
        header.Children.Add(create);
        var titles = new StackPanel { Spacing = LunetTokens.Space.Xxs, VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(LunetUi.Headline("Seus jogos"));
        if (_count.Parent is Panel oldParent) oldParent.Children.Remove(_count);
        titles.Children.Add(_count);
        header.Children.Add(titles);

        var content = new StackPanel { Margin = new Thickness(LunetTokens.Space.M, LunetTokens.Space.L) };
        content.Children.Add(header);
        content.Children.Add(Destinations());
        content.Children.Add(_grid);
        if (_trashRow.Parent is Panel oldTrash) oldTrash.Children.Remove(_trashRow);
        content.Children.Add(_trashRow);
        if (_overlay.Parent is Panel oldOverlay) oldOverlay.Children.Remove(_overlay);
        if (_snackbar.Parent is Panel oldBar) oldBar.Children.Remove(_snackbar);
        _snackbar.VerticalAlignment = VerticalAlignment.Bottom;
        Content = new Grid { Children = { new ScrollViewer { Content = content }, _snackbar, _overlay } };
        Refresh();
    }

    /// <summary>Where else to go from the Home (Jogar, Studios, Aprender…), one row that scrolls sideways.</summary>
    private Control Destinations()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = LunetTokens.Space.Xs };
        // Without the previous screens (desktop), only what lives in the shell is offered.
        foreach (var (route, label, icon) in LunetShell.HomeRoutes.Where(r => _shell.OpenRoute is not null || r.Route == "learn"))
        {
            // Icon tile with its label below (D5): six destinations read at a glance, one thumb-tap each.
            var tile = new StackPanel { Spacing = LunetTokens.Space.Xxs, Width = 72 };
            tile.Children.Add(new Border
            {
                Width = 56, Height = 56, CornerRadius = new CornerRadius(LunetTokens.Radius.L), HorizontalAlignment = HorizontalAlignment.Center,
                Background = LunetTokens.Brush(LunetTokens.Color.SurfaceRaised),
                Child = LunetIcons.Create(icon, LunetTokens.Color.Primary, 24),
            });
            var text = LunetUi.Text(label, LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
            text.TextAlignment = TextAlignment.Center; text.TextWrapping = TextWrapping.NoWrap;
            tile.Children.Add(text);
            var b = new Button
            {
                Content = tile, Padding = new Thickness(0), Background = LunetTokens.Brush("#00000000"), BorderThickness = new Thickness(0),
            };
            b.Classes.Add("lunet-card");
            AutomationProperties.SetName(b, label);
            // Aprender lives in the shell now (P3); the other destinations still open the previous screens.
            b.Click += (_, _) => { if (route == "learn") ShowLearn(); else _shell.OpenRoute?.Invoke(route, null); };
            b.Name = "Route-" + route;
            row.Children.Add(b);
        }
        return new ScrollViewer
        {
            Content = row, Margin = new Thickness(0, 0, 0, LunetTokens.Space.L),
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
    }

    /// <summary>The project names currently shown, in order (for tests and screen readers).</summary>
    public IReadOnlyList<string> Shown { get; private set; } = [];

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) > 1) Refresh(); // card columns follow the width
    }

    public void Refresh()
    {
        _grid.Children.Clear();
        Shown = _shell.Workspace.Projects();
        _count.Text = Shown.Count switch { 0 => "Comece criando um jogo", 1 => "1 jogo", _ => $"{Shown.Count} jogos" };
        if (Shown.Count == 0)
        {
            _grid.Children.Add(LunetUi.EmptyState(LunetIcons.Game, "Nenhum jogo ainda",
                "Crie um jogo: o código, a arte, o som e o jogo rodando ficam juntos, a um toque de distância.",
                LunetUi.Button("Criar o primeiro jogo", ButtonKind.Primary, LunetIcons.Plus, ShowCreate)));
        }
        else foreach (var name in Shown) _grid.Children.Add(ProjectCard(name));
        _trashRow.Children.Clear();
        var trash = _shell.Workspace.Store.Trash();
        if (trash.Count > 0)
        {
            var b = LunetUi.Button($"Lixeira ({trash.Count})", ButtonKind.Ghost, LunetIcons.Trash, ShowTrash);
            b.Name = "Trash";
            b.HorizontalAlignment = HorizontalAlignment.Left;
            _trashRow.Children.Add(b);
        }
    }

    /// <summary>A stable colour per game (its name hashed onto the accent family), so cards are told apart at a glance.</summary>
    public static string CardColor(string name)
    {
        string[] palette = ["#3b3270", "#27466b", "#2f5a4a", "#5a3b52", "#5a4a2b", "#2b4f5a"];
        uint h = 2166136261;
        foreach (var c in name) h = (h ^ c) * 16777619;
        return palette[h % (uint)palette.Length];
    }

    /// <summary>"editado agora", "há 5 min", "há 3 h", "ontem", "há 4 dias" or the date (D5).</summary>
    public static string Edited(DateTime utc, DateTime nowUtc)
    {
        var d = nowUtc - utc;
        if (d < TimeSpan.FromMinutes(1)) return "editado agora";
        if (d < TimeSpan.FromHours(1)) return $"editado há {(int)d.TotalMinutes} min";
        if (d < TimeSpan.FromDays(1)) return $"editado há {(int)d.TotalHours} h";
        if (d < TimeSpan.FromDays(2)) return "editado ontem";
        if (d < TimeSpan.FromDays(30)) return $"editado há {(int)d.TotalDays} dias";
        return "editado em " + utc.ToLocalTime().ToString("dd/MM/yyyy");
    }

    private Control ProjectCard(string name)
    {
        var folder = Path.Combine(_shell.Workspace.Root, name);
        var edited = Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Where(f => !f.Contains(Path.DirectorySeparatorChar + ".lunet" + Path.DirectorySeparatorChar))
                .Select(File.GetLastWriteTimeUtc).DefaultIfEmpty(Directory.GetLastWriteTimeUtc(folder)).Max()
            : DateTime.UtcNow;
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(new Border
        {
            Height = 92, CornerRadius = new CornerRadius(LunetTokens.Radius.M), Margin = new Thickness(0, 0, 0, LunetTokens.Space.Xs),
            Background = LunetTokens.Brush(CardColor(name)),
            Child = new Viewbox { Width = 36, Height = 36, Child = LunetIcons.Create(LunetIcons.Game, LunetTokens.Color.TextPrimary, 36) },
        });
        var title = LunetUi.Text(name, LunetTokens.Type.Body, weight: FontWeight.SemiBold);
        title.TextWrapping = TextWrapping.NoWrap; title.TextTrimming = TextTrimming.CharacterEllipsis;
        stack.Children.Add(title);
        stack.Children.Add(LunetUi.Text(Edited(edited, DateTime.UtcNow), LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary));
        var button = new Button
        {
            Content = LunetUi.Card(stack, LunetTokens.Space.S),
            Margin = new Thickness(0, 0, LunetTokens.Space.S, LunetTokens.Space.S),
            Padding = new Thickness(0), Background = LunetTokens.Brush("#00000000"), BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        // Two columns on a phone, as many 200 dp columns as fit on a wide screen.
        button.Width = Bounds.Width is > 0 and < 600 ? (Bounds.Width - 2 * LunetTokens.Space.M - LunetTokens.Space.S) / 2 - 1 : 200;
        button.Classes.Add("lunet-card");
        AutomationProperties.SetName(button, "Abrir " + name);
        button.Click += (_, _) => _shell.OpenProject(name);
        // Touch and hold (or right click) opens the same actions as the ⋮ button.
        button.ContextRequested += (_, e) => { ShowActions(name); e.Handled = true; };
        var more = LunetUi.IconButton(LunetIcons.More, "Ações de " + name, () => ShowActions(name));
        more.HorizontalAlignment = HorizontalAlignment.Right;
        more.VerticalAlignment = VerticalAlignment.Top;
        more.Margin = new Thickness(0, LunetTokens.Space.Xxs, LunetTokens.Space.S + LunetTokens.Space.Xxs, 0);
        // A dark disc behind the dots: readable on every cover colour.
        more.Content = new Border
        {
            Width = 32, Height = 32, CornerRadius = new CornerRadius(LunetTokens.Radius.Pill), Background = LunetTokens.Brush("#66000000"),
            Child = LunetIcons.Create(LunetIcons.More, LunetTokens.Color.TextPrimary, 20),
        };
        more.Name = "Actions-" + name;
        return new Panel { Width = button.Width + button.Margin.Right, Children = { button, more } };
    }

    // ---- Sheets: creating, renaming, the actions of a game and the trash ----

    /// <summary>The sheet open over the Home, or null.</summary>
    public Control? Sheet { get; private set; }

    /// <summary>The last message of the undo bar (tests and screen readers).</summary>
    public string? SnackbarText { get; private set; }

    /// <summary>Closes the open sheet; false when none was open (Back then leaves the Home as before).</summary>
    public bool CloseSheet()
    {
        if (Sheet is null) return false;
        if (Sheet is LearnView learn) return learn.Back(); // its pages first; at the catalog it calls CloseOverlay
        return CloseOverlay();
    }

    private bool CloseOverlay()
    {
        if (Sheet is null) return false;
        _overlay.Children.Clear();
        Sheet = null;
        return true;
    }

    private void Open(string title, Control body)
    {
        _overlay.Children.Clear();
        var compact = Bounds.Width is > 0 and < 600;
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, LunetTokens.Space.S) };
        var close = LunetUi.IconButton(LunetIcons.Close, "Fechar", () => CloseSheet());
        DockPanel.SetDock(close, Avalonia.Controls.Dock.Right);
        header.Children.Add(close);
        var t = LunetUi.Title(title); t.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(t);
        var card = new Border
        {
            Background = LunetTokens.Brush(LunetTokens.Color.SurfaceRaised),
            BorderBrush = LunetTokens.Brush(LunetTokens.Color.BorderStrong), BorderThickness = new Thickness(compact ? 0 : 1, 1, compact ? 0 : 1, compact ? 0 : 1),
            CornerRadius = compact ? new CornerRadius(LunetTokens.Radius.Xl, LunetTokens.Radius.Xl, 0, 0) : new CornerRadius(LunetTokens.Radius.Xl),
            Padding = new Thickness(LunetTokens.Space.L, LunetTokens.Space.M, LunetTokens.Space.M, LunetTokens.Space.L),
            Width = compact ? double.NaN : 440, MaxWidth = 520, MaxHeight = Bounds.Height > 0 ? Bounds.Height * 0.85 : double.PositiveInfinity,
            HorizontalAlignment = compact ? HorizontalAlignment.Stretch : HorizontalAlignment.Center,
            VerticalAlignment = compact ? VerticalAlignment.Bottom : VerticalAlignment.Center,
            BoxShadow = LunetTokens.Low,
            Child = new DockPanel { Children = { DockTop(header), new ScrollViewer { Content = body } } },
        };
        // A scrim that closes on tap: the sheet is light, nothing is lost by dismissing it.
        var scrim = new Border { Background = LunetTokens.Brush("#99000000") };
        scrim.PointerPressed += (_, _) => CloseSheet();
        _overlay.Children.Add(scrim);
        _overlay.Children.Add(card);
        Sheet = card;
    }

    private static Control DockTop(Control c) { DockPanel.SetDock(c, Avalonia.Controls.Dock.Top); return c; }

    /// <summary>Aprender, full screen over the Home (Back walks its pages, then closes it).</summary>
    public LearnView ShowLearn()
    {
        _overlay.Children.Clear();
        var learn = new LearnView(_shell, () => CloseOverlay());
        _overlay.Children.Add(learn);
        Sheet = learn;
        return learn;
    }

    /// <summary>A game's actions: Abrir, Renomear, Duplicar, Apagar.</summary>
    public void ShowActions(string name)
    {
        var list = new StackPanel { Spacing = LunetTokens.Space.Xxs };
        void Item(string label, string icon, Action action, string color = LunetTokens.Color.TextPrimary)
        {
            var b = LunetUi.Button(label, ButtonKind.Ghost, icon, action);
            b.HorizontalAlignment = HorizontalAlignment.Stretch;
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
            if (color != LunetTokens.Color.TextPrimary) foreach (var tb in b.GetLogicalDescendants().OfType<TextBlock>()) tb.Foreground = LunetTokens.Brush(color);
            b.Name = "Action-" + label;
            list.Children.Add(b);
        }
        Item("Abrir", LunetIcons.Play, () => { CloseSheet(); _shell.OpenProject(name); });
        Item("Renomear", LunetIcons.Edit, () => ShowRename(name));
        Item("Duplicar", LunetIcons.Copy, () => Run(() =>
        {
            var copy = _shell.DuplicateProject(name);
            CloseSheet();
            ShowSnackbar($"Cópia criada: {copy}", null);
        }));
        Item("Exportar ZIP", LunetIcons.Share, () => { CloseSheet(); _ = ExportZip(name); });
        Item("Apagar", LunetIcons.Trash, () => Delete(name), LunetTokens.Color.Danger);
        Open(name, list);
    }

    /// <summary>Moves a game to the trash at once, with "Desfazer" (reversible, so no confirmation).</summary>
    public void Delete(string name) => Run(() =>
    {
        var trashed = _shell.DeleteProject(name);
        CloseSheet();
        ShowSnackbar($"\"{name}\" foi para a lixeira", () => Run(() => _shell.RestoreProject(trashed.Id)));
    });

    public void ShowRename(string name)
    {
        var box = NameBox(name);
        var error = LunetUi.Text("", LunetTokens.Type.Caption, LunetTokens.Color.Danger);
        error.IsVisible = false;
        void Save()
        {
            try
            {
                _shell.RenameProject(name, box.Text ?? "");
                CloseSheet();
            }
            catch (Exception ex) when (ex is ProjectException or IOException or UnauthorizedAccessException)
            {
                error.Text = ex.Message; error.IsVisible = true;
            }
        }
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Save(); e.Handled = true; } };
        var body = new StackPanel { Spacing = LunetTokens.Space.S };
        body.Children.Add(box);
        body.Children.Add(error);
        body.Children.Add(LunetUi.Text("O jogo instalado a partir dele continua atualizando: a identidade do APK não muda.", LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary));
        body.Children.Add(Actions(LunetUi.Button("Salvar", ButtonKind.Primary, null, Save)));
        Open("Renomear", body);
        box.AttachedToVisualTree += (_, _) => { box.Focus(); box.SelectAll(); };
    }

    /// <summary>The starting points a new game can have (the templates of the core).</summary>
    public static readonly IReadOnlyList<(ProjectTemplate Template, string Title, string Body)> Templates =
    [
        (ProjectTemplate.Blank, "Vazio", "Só o esqueleto do jogo: Update, Draw e um SpriteBatch."),
        (ProjectTemplate.CoinCatcher, "Pegar moedas", "Um jogo pequeno e completo para mexer e aprender."),
        (ProjectTemplate.Animation, "Animação", "Sprites animados e tween ao tocar."),
        (ProjectTemplate.Particles, "Partículas", "Explosões e fluxo contínuo de partículas."),
        (ProjectTemplate.Lab, "Laboratório", "Toque, sensores, som e vibração do aparelho, para testar."),
    ];

    /// <summary>"Novo jogo": a name (already filled), where to start and the orientation. One tap on Criar.</summary>
    public void ShowCreate()
    {
        var box = NameBox(_shell.NextProjectName());
        var template = ProjectTemplate.Blank;
        var orientation = "landscape";
        var error = LunetUi.Text("", LunetTokens.Type.Caption, LunetTokens.Color.Danger);
        error.IsVisible = false;

        var templates = new StackPanel { Spacing = LunetTokens.Space.Xs };
        void DrawTemplates()
        {
            templates.Children.Clear();
            foreach (var (t, title, text) in Templates)
            {
                var selected = t == template;
                var stack = new StackPanel { Spacing = 2 };
                stack.Children.Add(LunetUi.Text(title, LunetTokens.Type.Callout, selected ? LunetTokens.Color.Primary : LunetTokens.Color.TextPrimary, FontWeight.SemiBold));
                stack.Children.Add(LunetUi.Text(text, LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary));
                var b = new Button
                {
                    Content = stack, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(LunetTokens.Space.S), MinHeight = LunetTokens.MinTouch,
                    Background = LunetTokens.Brush(selected ? LunetTokens.Color.PrimarySoft : LunetTokens.Color.Surface),
                    BorderBrush = LunetTokens.Brush(selected ? LunetTokens.Color.Primary : LunetTokens.Color.Border), BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(LunetTokens.Radius.M), Name = "Template-" + t,
                };
                AutomationProperties.SetName(b, title + (selected ? ", escolhido" : ""));
                var chosen = t;
                b.Click += (_, _) => { template = chosen; DrawTemplates(); };
                templates.Children.Add(b);
            }
        }
        DrawTemplates();

        var orientations = new StackPanel { Orientation = Orientation.Horizontal, Spacing = LunetTokens.Space.Xs };
        void DrawOrientations()
        {
            orientations.Children.Clear();
            foreach (var (value, label) in new[] { ("landscape", "Paisagem"), ("portrait", "Retrato") })
            {
                var b = LunetUi.Button(label, value == orientation ? ButtonKind.Primary : ButtonKind.Secondary, null, () => { orientation = value; DrawOrientations(); });
                b.Name = "Orientation-" + value;
                orientations.Children.Add(b);
            }
        }
        DrawOrientations();

        void Create()
        {
            try
            {
                _shell.CreateProject(box.Text ?? "", template, orientation);
                CloseSheet();
            }
            catch (Exception ex) when (ex is ProjectException or IOException or UnauthorizedAccessException)
            {
                error.Text = ex.Message; error.IsVisible = true;
            }
        }
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Create(); e.Handled = true; } };

        var body = new StackPanel { Spacing = LunetTokens.Space.S };
        body.Children.Add(Label("Nome"));
        body.Children.Add(box);
        body.Children.Add(error);
        body.Children.Add(Label("Começar com"));
        body.Children.Add(templates);
        body.Children.Add(Label("Tela"));
        body.Children.Add(orientations);
        var create = LunetUi.Button("Criar", ButtonKind.Primary, LunetIcons.Plus, Create);
        create.Name = "CreateGame";
        var import = LunetUi.Button("Importar ZIP", ButtonKind.Ghost, LunetIcons.Download, () => { CloseSheet(); _ = ImportZip(); });
        import.Name = "ImportZip";
        var row = new DockPanel { Margin = new Thickness(0, LunetTokens.Space.Xs, 0, 0) };
        DockPanel.SetDock(create, Avalonia.Controls.Dock.Right);
        row.Children.Add(create);
        import.HorizontalAlignment = HorizontalAlignment.Left;
        row.Children.Add(import);
        body.Children.Add(row);
        Open("Novo jogo", body);
    }

    /// <summary>
    /// Saves a game as a ZIP where the person chooses (Android: the system's document picker, so Drive or Downloads;
    /// desktop: the save dialog). The file is written off the UI thread.
    /// </summary>
    public async Task ExportZip(string name)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanSave: true } storage) { ShowSnackbar("Este aparelho não oferece onde salvar arquivos.", null); return; }
        var file = await storage.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Exportar " + name, SuggestedFileName = name + ".zip", DefaultExtension = "zip",
            FileTypeChoices = [ZipType],
        });
        if (file is null) return;
        try
        {
            await using var stream = await file.OpenWriteAsync();
            await Task.Run(() => _shell.ExportProjectZip(name, stream));
            ShowSnackbar($"\"{name}\" exportado como ZIP", null);
            Haptics.Confirm?.Invoke();
        }
        catch (Exception ex) when (ex is ProjectException or IOException or UnauthorizedAccessException)
        {
            ShowSnackbar("Não deu para exportar: " + ex.Message, null);
        }
    }

    /// <summary>Brings a game in from a ZIP the person picks; nothing appears until every file was checked.</summary>
    public async Task ImportZip()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage) { ShowSnackbar("Este aparelho não oferece como abrir arquivos.", null); return; }
        var files = await storage.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Importar jogo (ZIP)", AllowMultiple = false,
            // Some providers report ZIP as octet-stream; the core checks the content.
            FileTypeFilter = [ZipType, Avalonia.Platform.Storage.FilePickerFileTypes.All],
        });
        if (files.Count == 0) return;
        ShowSnackbar("Importando…", null);
        try
        {
            await using var stream = await files[0].OpenReadAsync();
            var name = await Task.Run(() => _shell.ImportProjectZip(stream));
            Refresh();
            ShowSnackbar($"Jogo importado: {name}", null);
            Haptics.Confirm?.Invoke();
        }
        catch (Exception ex) when (ex is ProjectException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ShowSnackbar("Não deu para importar: " + ex.Message, null);
        }
    }

    private static readonly Avalonia.Platform.Storage.FilePickerFileType ZipType = new("Jogo do Lunet (ZIP)")
    {
        Patterns = ["*.zip"], MimeTypes = ["application/zip", "application/x-zip-compressed"],
    };

    /// <summary>The trash: restore a game, or erase it for good (asks once, inline: it cannot be undone).</summary>
    public void ShowTrash()
    {
        var list = new StackPanel { Spacing = LunetTokens.Space.Xs };
        var trash = _shell.Workspace.Store.Trash();
        if (trash.Count == 0) list.Children.Add(LunetUi.Secondary("A lixeira está vazia."));
        foreach (var item in trash)
        {
            var row = new DockPanel { MinHeight = LunetTokens.MinTouch };
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = LunetTokens.Space.Xxs };
            var restore = LunetUi.Button("Restaurar", ButtonKind.Secondary, null, () => Run(() =>
            {
                var name = _shell.RestoreProject(item.Id);
                ShowTrash();
                ShowSnackbar($"\"{name}\" voltou para os seus jogos", null);
            }));
            restore.Name = "Restore-" + item.Id;
            var armed = false;
            Button? purge = null;
            purge = LunetUi.Button("Apagar de vez", ButtonKind.Ghost, null, () =>
            {
                if (!armed)
                {
                    armed = true;
                    foreach (var tb in purge!.GetLogicalDescendants().OfType<TextBlock>()) { tb.Text = "Tocar de novo apaga"; tb.Foreground = LunetTokens.Brush(LunetTokens.Color.Danger); }
                    return;
                }
                Run(() => { _shell.PurgeProject(item.Id); ShowTrash(); });
            });
            purge.Name = "Purge-" + item.Id;
            actions.Children.Add(restore);
            actions.Children.Add(purge);
            DockPanel.SetDock(actions, Avalonia.Controls.Dock.Right);
            row.Children.Add(actions);
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var title = LunetUi.Text(item.Name, LunetTokens.Type.Callout, weight: FontWeight.SemiBold);
            title.TextTrimming = TextTrimming.CharacterEllipsis; title.TextWrapping = TextWrapping.NoWrap;
            text.Children.Add(title);
            text.Children.Add(LunetUi.Text(Edited(item.DeletedUtc, DateTime.UtcNow).Replace("editado", "apagado"), LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary));
            row.Children.Add(text);
            list.Children.Add(row);
        }
        list.Children.Add(LunetUi.Text("Jogos na lixeira ficam na pasta de projetos, em .lixeira, até você apagá-los de vez.", LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary));
        Open("Lixeira", list);
    }

    /// <summary>The undo bar: a message and, when the action can be undone, "Desfazer".</summary>
    public void ShowSnackbar(string text, Action? undo)
    {
        SnackbarText = text;
        var row = new DockPanel();
        if (undo is not null)
        {
            var b = LunetUi.Button("Desfazer", ButtonKind.Ghost, null, () => { HideSnackbar(); undo(); });
            b.Name = "Undo";
            foreach (var tb in b.GetLogicalDescendants().OfType<TextBlock>()) tb.Foreground = LunetTokens.Brush(LunetTokens.Color.Primary);
            DockPanel.SetDock(b, Avalonia.Controls.Dock.Right);
            row.Children.Add(b);
        }
        var message = LunetUi.Text(text, LunetTokens.Type.Callout);
        message.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(message);
        _snackbar.Child = row;
        _snackbar.Background = LunetTokens.Brush(LunetTokens.Color.SurfaceOverlay);
        _snackbar.CornerRadius = new CornerRadius(LunetTokens.Radius.M);
        _snackbar.Padding = new Thickness(LunetTokens.Space.M, LunetTokens.Space.Xxs, LunetTokens.Space.Xxs, LunetTokens.Space.Xxs);
        _snackbar.Margin = new Thickness(LunetTokens.Space.M);
        _snackbar.MinHeight = LunetTokens.MinTouch;
        _snackbar.MaxWidth = 560;
        _snackbar.BoxShadow = LunetTokens.Low;
        _snackbar.IsVisible = true;
        _snackbarTimer.Stop();
        _snackbarTimer.Start();
    }

    private void HideSnackbar()
    {
        _snackbarTimer.Stop();
        _snackbar.IsVisible = false;
    }

    /// <summary>Runs a file action; a failure (disk, permission, name) is said in the undo bar, never lost.</summary>
    private void Run(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is ProjectException or IOException or UnauthorizedAccessException)
        {
            ShowSnackbar(ex.Message, null);
        }
    }

    private static TextBox NameBox(string text)
    {
        var box = new TextBox
        {
            Text = text, MinHeight = LunetTokens.MinTouch, FontFamily = LunetTokens.UiFont, FontSize = LunetTokens.Type.Body,
            VerticalContentAlignment = VerticalAlignment.Center, MaxLength = 60, Name = "ProjectName",
        };
        AutomationProperties.SetName(box, "Nome do jogo");
        return box;
    }

    private static TextBlock Label(string text) => LunetUi.Text(text, LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary, FontWeight.SemiBold);

    private static Control Actions(Control primary)
    {
        primary.HorizontalAlignment = HorizontalAlignment.Right;
        primary.Margin = new Thickness(0, LunetTokens.Space.Xs, 0, 0);
        return primary;
    }
}
