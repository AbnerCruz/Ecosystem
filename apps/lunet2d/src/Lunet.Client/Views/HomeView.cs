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

    /// <summary>A phone: one game per row (list); wider, cards in a grid.</summary>
    private bool Compact => Bounds.Width is > 0 and < 600 || Bounds.Width <= 0;
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

    /// <summary>The Home's destinations in the bottom navigation bar (Material 3: 3–5, in the thumb's reach).</summary>
    public enum Destination { Games, Learn, Play, Studios, More, Library }

    private readonly StackPanel _nav = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Grid _navGrid = new();

    /// <summary>The destination selected in the bottom bar.</summary>
    public Destination Selected { get; private set; } = Destination.Games;

    /// <summary>Lays the Home out (again when the head sets <see cref="LunetShell.OpenRoute"/>).</summary>
    public void Build()
    {
        if (_grid.Parent is Panel old) old.Children.Remove(_grid);
        if (_count.Parent is Panel oldParent) oldParent.Children.Remove(_count);
        var titles = new StackPanel { Spacing = LunetTokens.Space.Xxs, Margin = new Thickness(0, LunetTokens.Space.S, 0, LunetTokens.Space.M) };
        titles.Children.Add(LunetUi.Headline("Seus jogos"));
        titles.Children.Add(_count);

        var content = new StackPanel { Margin = new Thickness(LunetTokens.Space.M, LunetTokens.Space.L, LunetTokens.Space.M, 96) };
        content.Children.Add(titles);
        content.Children.Add(_grid);
        if (_trashRow.Parent is Panel oldTrash) oldTrash.Children.Remove(_trashRow);
        content.Children.Add(_trashRow);
        if (_overlay.Parent is Panel oldOverlay) oldOverlay.Children.Remove(_overlay);
        if (_snackbar.Parent is Panel oldBar) oldBar.Children.Remove(_snackbar);
        _snackbar.VerticalAlignment = VerticalAlignment.Bottom;

        // "Novo jogo" floats bottom-right, where the thumb rests (Material extended FAB).
        var create = LunetUi.Button("Novo jogo", ButtonKind.Primary, LunetIcons.Plus, ShowCreate);
        create.Name = "NewProject";
        create.MinHeight = 56;
        create.CornerRadius = new CornerRadius(LunetTokens.Radius.L);
        create.HorizontalAlignment = HorizontalAlignment.Right;
        create.VerticalAlignment = VerticalAlignment.Bottom;
        create.Margin = new Thickness(LunetTokens.Space.M);

        var page = new Grid { Children = { new ScrollViewer { Content = content }, create, _snackbar, _overlay } };
        BuildNav();
        if (_navGrid.Parent is Border oldNav) oldNav.Child = null;
        var nav = new Border
        {
            Child = _navGrid, Background = LunetTokens.Brush(LunetTokens.Color.Surface),
            BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border), BorderThickness = new Thickness(0, 1, 0, 0),
        };
        var root = new DockPanel();
        DockPanel.SetDock(nav, Avalonia.Controls.Dock.Bottom);
        root.Children.Add(nav);
        root.Children.Add(page);
        Content = root;
        Refresh();
    }

    /// <summary>
    /// The bottom bar: Jogos, Aprender (in the shell), Jogar and Studios (still the previous screens) and Mais
    /// (Biblioteca, Explorar, Ajustes). Without the previous screens (desktop) only what lives in the shell appears.
    /// </summary>
    private void BuildNav()
    {
        var items = new List<(Destination Destination, string Label, string Icon)> { (Destination.Games, "Jogos", LunetIcons.Game), (Destination.Learn, "Aprender", LunetIcons.Book) };
        items.Add((Destination.Play, "Jogar", LunetIcons.Play));
        if (_shell.OpenRoute is not null)
        {
            items.Add((Destination.Studios, "Studios", LunetIcons.Art));
            items.Add((Destination.More, "Mais", LunetIcons.More));
        }
        else items.Add((Destination.Library, "Biblioteca", LunetIcons.Folder));
        _navGrid.Children.Clear();
        _navGrid.ColumnDefinitions = new ColumnDefinitions(string.Join(",", items.Select(_ => "*")));
        for (var i = 0; i < items.Count; i++)
        {
            var (destination, label, icon) = items[i];
            var tab = LunetUi.Tab(icon, label, destination == Selected, () => Go(destination));
            tab.Name = "Nav-" + destination;
            Grid.SetColumn(tab, i);
            _navGrid.Children.Add(tab);
        }
    }

    /// <summary>Goes to a destination of the bottom bar (the same path as a tap).</summary>
    public void Go(Destination destination)
    {
        switch (destination)
        {
            case Destination.Games: CloseOverlay(); break;
            case Destination.Learn: ShowLearn(); break;
            case Destination.Play: ShowPlay(); break;
            case Destination.Studios: _shell.OpenRoute?.Invoke("studios", null); break;
            case Destination.More: ShowMore(); break;
            case Destination.Library: ShowLibrary(); break;
        }
    }

    private void Select(Destination destination)
    {
        if (Selected == destination) return;
        Selected = destination;
        BuildNav();
    }

    /// <summary>Mais: the destinations that still live in the previous screens.</summary>
    private void ShowMore()
    {
        var list = new StackPanel { Spacing = LunetTokens.Space.Xxs };
        foreach (var (route, label, icon, detail) in new[]
        {
            ("library", "Biblioteca", LunetIcons.Folder, "Recursos seus para usar em qualquer jogo"),
            ("explore", "Explorar", LunetIcons.Map, "Referências e inspiração"),
            ("settings", "Ajustes", LunetIcons.Speed, "Aparência, pasta de projetos, IA"),
        })
        {
            var stack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(LunetUi.Text(label, LunetTokens.Type.Callout, weight: FontWeight.SemiBold));
            stack.Children.Add(LunetUi.Text(detail, LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary));
            var row = new DockPanel();
            var ic = LunetIcons.Create(icon, LunetTokens.Color.Primary, 22);
            ic.Margin = new Thickness(0, 0, LunetTokens.Space.M, 0);
            DockPanel.SetDock(ic, Avalonia.Controls.Dock.Left);
            row.Children.Add(ic);
            row.Children.Add(stack);
            var b = new Button
            {
                Content = row, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                MinHeight = 56, Padding = new Thickness(LunetTokens.Space.S), Background = LunetTokens.Brush("#00000000"), BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(LunetTokens.Radius.M), Name = "Route-" + route,
            };
            AutomationProperties.SetName(b, label);
            var r = route;
            // The Biblioteca lives in the shell now (A6); Explorar and Ajustes still open the previous screens.
            b.Click += (_, _) => { CloseOverlay(); if (r == "library") ShowLibrary(); else _shell.OpenRoute?.Invoke(r, null); };
            list.Children.Add(b);
        }
        Open("Mais", list);
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
        else foreach (var name in Shown) _grid.Children.Add(Compact ? ProjectRow(name) : ProjectCard(name));
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
        // ZIP imports and shared-folder copies stamp files with a fixed old date: say nothing rather than a false date.
        if (utc.Year < 2010 || utc > nowUtc.AddDays(1)) return "";
        var d = nowUtc - utc;
        if (d < TimeSpan.FromMinutes(1)) return "editado agora";
        if (d < TimeSpan.FromHours(1)) return $"editado há {(int)d.TotalMinutes} min";
        if (d < TimeSpan.FromDays(1)) return $"editado há {(int)d.TotalHours} h";
        if (d < TimeSpan.FromDays(2)) return "editado ontem";
        if (d < TimeSpan.FromDays(30)) return $"editado há {(int)d.TotalDays} dias";
        return "editado em " + utc.ToLocalTime().ToString("dd/MM/yyyy");
    }

    private DateTime LastEdited(string name)
    {
        var folder = Path.Combine(_shell.Workspace.Root, name);
        if (!Directory.Exists(folder)) return DateTime.MinValue;
        try
        {
            return Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + ".lunet" + Path.DirectorySeparatorChar))
                .Select(File.GetLastWriteTimeUtc).DefaultIfEmpty(Directory.GetLastWriteTimeUtc(folder)).Max();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return DateTime.MinValue; }
    }

    private static Control Cover(string name, double size, double radius) => new Border
    {
        Width = size, Height = size, CornerRadius = new CornerRadius(radius), Background = LunetTokens.Brush(CardColor(name)),
        Child = new TextBlock
        {
            Text = name.Length > 0 ? char.ToUpperInvariant(name[0]).ToString() : "?", FontFamily = LunetTokens.UiFont,
            FontSize = size * 0.42, FontWeight = FontWeight.Bold, Foreground = LunetTokens.Brush(LunetTokens.Color.TextPrimary),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        },
    };

    private Button MoreButton(string name)
    {
        var more = LunetUi.IconButton(LunetIcons.More, "Ações de " + name, () => ShowActions(name), LunetTokens.Color.TextSecondary);
        more.Name = "Actions-" + name;
        return more;
    }

    /// <summary>A phone row (72 dp): cover, name, last edit, and ⋮ in the thumb's reach on the right.</summary>
    private Control ProjectRow(string name)
    {
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(LunetTokens.Space.M, 0, 0, 0) };
        var title = LunetUi.Text(name, LunetTokens.Type.Body, weight: FontWeight.SemiBold);
        title.TextWrapping = TextWrapping.NoWrap; title.TextTrimming = TextTrimming.CharacterEllipsis;
        text.Children.Add(title);
        var edited = Edited(LastEdited(name), DateTime.UtcNow);
        if (edited.Length > 0) text.Children.Add(LunetUi.Text(edited, LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary));
        var body = new DockPanel();
        var cover = Cover(name, 48, LunetTokens.Radius.M);
        DockPanel.SetDock(cover, Avalonia.Controls.Dock.Left);
        body.Children.Add(cover);
        body.Children.Add(text);
        var open = new Button
        {
            Content = body, MinHeight = 72, Padding = new Thickness(LunetTokens.Space.S, LunetTokens.Space.Xs, 56, LunetTokens.Space.Xs),
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = LunetTokens.Brush("#00000000"), BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(LunetTokens.Radius.L),
        };
        AutomationProperties.SetName(open, "Abrir " + name);
        open.Click += (_, _) => _shell.OpenProject(name);
        open.ContextRequested += (_, e) => { ShowActions(name); e.Handled = true; };
        var more = MoreButton(name);
        more.HorizontalAlignment = HorizontalAlignment.Right;
        more.VerticalAlignment = VerticalAlignment.Center;
        more.Margin = new Thickness(0, 0, LunetTokens.Space.Xxs, 0);
        // The row fills the width (WrapPanel would size it to its content).
        var width = Bounds.Width > 0 ? Bounds.Width - 2 * LunetTokens.Space.M : double.NaN;
        return new Panel { Width = width, Children = { open, more } };
    }

    /// <summary>A card of the grid on wide screens.</summary>
    private Control ProjectCard(string name)
    {
        var stack = new StackPanel { Spacing = 2 };
        var cover = Cover(name, 96, LunetTokens.Radius.M);
        ((Border)cover).Width = double.NaN; ((Border)cover).Height = 112;
        ((TextBlock)((Border)cover).Child!).FontSize = 40;
        cover.Margin = new Thickness(0, 0, 0, LunetTokens.Space.S);
        stack.Children.Add(cover);
        var title = LunetUi.Text(name, LunetTokens.Type.Body, weight: FontWeight.SemiBold);
        title.TextWrapping = TextWrapping.NoWrap; title.TextTrimming = TextTrimming.CharacterEllipsis;
        stack.Children.Add(title);
        var edited = Edited(LastEdited(name), DateTime.UtcNow);
        stack.Children.Add(LunetUi.Text(edited.Length > 0 ? edited : " ", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary));
        var open = new Button
        {
            Content = LunetUi.Card(stack, LunetTokens.Space.S), Width = 220,
            Padding = new Thickness(0), Background = LunetTokens.Brush("#00000000"), BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        open.Classes.Add("lunet-card");
        AutomationProperties.SetName(open, "Abrir " + name);
        open.Click += (_, _) => _shell.OpenProject(name);
        open.ContextRequested += (_, e) => { ShowActions(name); e.Handled = true; };
        var more = MoreButton(name);
        more.HorizontalAlignment = HorizontalAlignment.Right;
        more.VerticalAlignment = VerticalAlignment.Bottom;
        more.Margin = new Thickness(0, 0, LunetTokens.Space.Xxs, LunetTokens.Space.Xxs);
        return new Panel { Margin = new Thickness(0, 0, LunetTokens.Space.S, LunetTokens.Space.S), Children = { open, more } };
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
        if (Sheet is LibraryView library) return library.Back();
        if (Sheet is PlayView) return CloseOverlay();
        return CloseOverlay();
    }

    private bool CloseOverlay()
    {
        if (Sheet is null) return false;
        _overlay.Children.Clear();
        Sheet = null;
        Select(Destination.Games);
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
        Select(Destination.Learn);
        return learn;
    }

    /// <summary>Jogar, full screen over the Home (P4).</summary>
    public PlayView ShowPlay()
    {
        _overlay.Children.Clear();
        var play = new PlayView(_shell, () => CloseOverlay());
        _overlay.Children.Add(play);
        Sheet = play;
        Select(Destination.Play);
        return play;
    }

    /// <summary>The Biblioteca, full screen over the Home (Back walks its pages, then closes it).</summary>
    public LibraryView ShowLibrary()
    {
        _overlay.Children.Clear();
        var library = new LibraryView(_shell, () => CloseOverlay());
        _overlay.Children.Add(library);
        Sheet = library;
        Select(_shell.OpenRoute is null ? Destination.Library : Destination.More);
        return library;
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
