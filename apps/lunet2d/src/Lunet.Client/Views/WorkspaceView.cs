using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lunet.Client.Shell;
using Lunet.Client.Ui;

namespace Lunet.Client.Views;

/// <summary>
/// Level 2 of the shell: one game, with its views as tabs (Código · Jogo · Arte · Mapa · Som · Notas) and the support
/// panels (Console, Problemas) as a bottom sheet on a phone or a side column on a wide screen. No main flow uses a modal
/// dialog. The heavy controls (editor, game) are created once and moved between layouts, so nothing is lost on resize.
/// </summary>
public sealed class WorkspaceView : UserControl
{
    private static readonly (WorkspaceTab Tab, string Icon, string Label)[] Tabs =
    [
        (WorkspaceTab.Code, LunetIcons.Code, "Código"),
        (WorkspaceTab.Game, LunetIcons.Game, "Jogo"),
        (WorkspaceTab.Art, LunetIcons.Art, "Arte"),
        (WorkspaceTab.Map, LunetIcons.Map, "Mapa"),
        (WorkspaceTab.Sound, LunetIcons.Sound, "Som"),
        (WorkspaceTab.Notes, LunetIcons.Notes, "Notas"),
    ];

    private readonly LunetShell _shell;
    private readonly DocumentEditor _code;
    private readonly DocumentEditor _notes;
    private readonly GameView _game;
    private readonly SupportPanelView _support;
    private readonly TextBlock _title = LunetUi.Title("");
    private Control? _tabBar, _sheet;
    private readonly Border _topHost = new();

    /// <summary>Redraws only the top bar (state, problems), never moving the editor (that would close the keyboard).</summary>
    public void RefreshTopBar() => _topHost.Child = TopBar();
    private bool _keyboardOpen;

    /// <summary>
    /// While the phone's keyboard is open the tab bar and the bottom sheet step aside, so the code, the code bar and
    /// the caret keep the space (changes visibility only: re-parenting the editor would close the keyboard).
    /// </summary>
    public void SetKeyboardOpen(bool open)
    {
        _keyboardOpen = open;
        if (_tabBar is not null) _tabBar.IsVisible = !open;
        if (_sheet is not null) _sheet.IsVisible = !open;
    }

    public bool TabBarVisible => _tabBar?.IsVisible ?? false;

    public WorkspaceView(LunetShell shell)
    {
        _shell = shell;
        _code = new DocumentEditor(shell, WorkspaceTab.Code);
        _notes = new DocumentEditor(shell, WorkspaceTab.Notes);
        _game = new GameView(shell);
        _support = new SupportPanelView(shell);
        _title.TextWrapping = TextWrapping.NoWrap;
        _title.TextTrimming = TextTrimming.CharacterEllipsis;
        _title.VerticalAlignment = VerticalAlignment.Center;
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
    }

    public GameView Game => _game;
    public DocumentEditor Code => _code;
    public DocumentEditor Notes => _notes;
    public SupportPanelView Support => _support;

    /// <summary>Opens the project's documents (entry point in Código, first note in Notas).</summary>
    public void Load()
    {
        _title.Text = _shell.State.Project ?? "";
        _code.OpenSession(_shell.Workspace.Project!.Manifest.EntryPoint);
        _notes.Load(null);
        Rebuild();
    }

    /// <summary>Reloads the open documents from disk (another screen may have changed them); unsaved edits here win.</summary>
    public void ReloadFromDisk()
    {
        if (!_code.IsDirty) _code.Load(_code.OpenPath);
        if (!_notes.IsDirty) _notes.Load(_notes.OpenPath);
        Rebuild();
    }

    /// <summary>Saves every open document (before running, leaving or closing). Verified write, never memory-only.</summary>
    public void SaveAll()
    {
        _code.Save();
        _notes.Save();
        _code.SaveSession();
    }

    /// <summary>Lays the workspace out for the current state (layout, tab, panel).</summary>
    public void Rebuild()
    {
        _tabBar = _sheet = null;
        foreach (var c in new Control[] { _code, _notes, _game, _support }) Detach(c);
        _code.Relayout();
        _notes.Relayout();
        var state = _shell.State;
        var expanded = state.Layout == ShellLayout.Expanded;

        var root = new DockPanel();
        if (_topHost.Parent is Panel oldTop) oldTop.Children.Remove(_topHost);
        _topHost.Child = TopBar();
        var top = _topHost;
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);

        if (expanded)
        {
            var rail = new StackPanel { Spacing = LunetTokens.Space.Xxs, Width = 80, Margin = new Thickness(0, LunetTokens.Space.Xs) };
            foreach (var (tab, icon, label) in Tabs) rail.Children.Add(LunetUi.Tab(icon, label, IsSelected(tab), () => state.SelectTab(tab)));
            var railHost = new Border { Child = rail, Background = LunetTokens.Brush(LunetTokens.Color.Surface), BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border), BorderThickness = new Thickness(0, 0, 1, 0) };
            DockPanel.SetDock(railHost, Dock.Left);
            root.Children.Add(railHost);
            if (state.Panel != SupportPanel.None)
            {
                var column = new Border
                {
                    Width = 380, Child = _support, Background = LunetTokens.Brush(LunetTokens.Color.Surface),
                    BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border), BorderThickness = new Thickness(1, 0, 0, 0),
                };
                DockPanel.SetDock(column, Dock.Right);
                root.Children.Add(column);
            }
            root.Children.Add(Body(expanded));
        }
        else
        {
            var bar = new Avalonia.Controls.Primitives.UniformGrid { Rows = 1, Columns = Tabs.Length };
            foreach (var (tab, icon, label) in Tabs) bar.Children.Add(LunetUi.Tab(icon, label, IsSelected(tab), () => state.SelectTab(tab)));
            var barHost = new Border { Child = bar, Background = LunetTokens.Brush(LunetTokens.Color.Surface), BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border), BorderThickness = new Thickness(0, 1, 0, 0) };
            DockPanel.SetDock(barHost, Dock.Bottom);
            _tabBar = barHost;
            barHost.IsVisible = !_keyboardOpen;
            root.Children.Add(barHost);
            if (state.Panel != SupportPanel.None)
            {
                // Bottom sheet: the content stays visible above it (no scrim, no modal).
                var sheet = new Border
                {
                    // Inspector, Desempenho and the AI need room: half the screen, the game still above it.
                    Height = state.Panel is SupportPanel.Inspector or SupportPanel.Performance or SupportPanel.Ai or SupportPanel.Docs && Bounds.Height > 0
                        ? Math.Max(280, Math.Round(Bounds.Height * 0.5)) : 280,
                    Child = _support, Background = LunetTokens.Brush(LunetTokens.Color.SurfaceRaised),
                    CornerRadius = new CornerRadius(LunetTokens.Radius.Xl, LunetTokens.Radius.Xl, 0, 0),
                    BorderBrush = LunetTokens.Brush(LunetTokens.Color.BorderStrong), BorderThickness = new Thickness(0, 1, 0, 0),
                    BoxShadow = LunetTokens.Low,
                };
                DockPanel.SetDock(sheet, Dock.Bottom);
                _sheet = sheet;
                sheet.IsVisible = !_keyboardOpen;
                root.Children.Add(sheet);
            }
            root.Children.Add(Body(expanded));
        }
        _support.Refresh();
        Content = root;
    }

    private bool IsSelected(WorkspaceTab tab) => _shell.State.CodeAndGameSideBySide
        ? tab is WorkspaceTab.Code or WorkspaceTab.Game
        : _shell.State.Tab == tab;

    private Control Body(bool expanded)
    {
        var state = _shell.State;
        if (state.CodeAndGameSideBySide)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,1,*") };
            grid.Children.Add(_code);
            var divider = LunetUi.Divider(vertical: true); Grid.SetColumn(divider, 1); grid.Children.Add(divider);
            Grid.SetColumn(_game, 2); grid.Children.Add(_game);
            Grid.SetColumn(_code, 0);
            return grid;
        }
        Grid.SetColumn(_code, 0); Grid.SetColumn(_game, 0);
        return state.Tab switch
        {
            WorkspaceTab.Code => _code,
            WorkspaceTab.Game => _game,
            WorkspaceTab.Notes => _notes,
            _ => AssetsView(state.Tab),
        };
    }

    private Control TopBar()
    {
        var bar = new DockPanel { Height = 56, Margin = new Thickness(LunetTokens.Space.Xxs, 0, LunetTokens.Space.Xs, 0) };
        var back = LunetUi.IconButton(LunetIcons.Back, "Voltar aos jogos", () => _shell.GoHome());
        DockPanel.SetDock(back, Dock.Left);
        bar.Children.Add(back);

        // M3 top app bar (LUNET-537 D3): back, the game and its state, one panels button and the primary action.
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = LunetTokens.Space.Xxs, VerticalAlignment = VerticalAlignment.Center };
        var liveErrors = _shell.LiveProblems.Count(p => p.Severity == Lunet.Compiler.DiagnosticSeverity.Error);
        var problems = _shell.Game.Problems.Count > 0 ? _shell.Game.Problems.Count : liveErrors;
        var panels = LunetUi.IconButton(LunetIcons.More, problems > 0 ? $"Painéis — {problems} problema(s)" : "Painéis",
            () => _shell.State.TogglePanel(SupportPanel.More),
            _shell.State.Panel != SupportPanel.None ? LunetTokens.Color.Primary : LunetTokens.Color.TextPrimary);
        panels.Name = "PanelsButton";
        if (problems > 0)
        {
            // A small red dot, like a notification badge, instead of a second icon.
            var host = new Panel { Children = { panels, new Border
            {
                Width = 10, Height = 10, CornerRadius = new CornerRadius(5), Background = LunetTokens.Brush(LunetTokens.Color.Danger),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 10, 10, 0),
                IsHitTestVisible = false,
            } } };
            actions.Children.Add(host);
        }
        else actions.Children.Add(panels);
        var running = _shell.Game.State is GameState.Running or GameState.Paused;
        var run = running
            ? LunetUi.Button("Parar", ButtonKind.Secondary, LunetIcons.Stop, () => _shell.StopGame())
            : LunetUi.Button("Rodar", ButtonKind.Primary, LunetIcons.Play, () => _shell.RunGame());
        run.Name = "RunButton";
        run.Margin = new Thickness(LunetTokens.Space.Xs, 0, 0, 0);
        actions.Children.Add(run);
        DockPanel.SetDock(actions, Dock.Right);
        bar.Children.Add(actions);
        _title.Margin = new Thickness(0);
        Detach(_title);
        var status = LunetUi.Text(_shell.Game.State switch
        {
            GameState.Running => "Rodando",
            GameState.Paused => "Pausado",
            GameState.CompileErrors => problems == 1 ? "1 problema" : $"{problems} problemas",
            GameState.Faulted => "Parou com erro",
            _ when liveErrors > 0 => liveErrors == 1 ? "1 erro no código" : $"{liveErrors} erros no código",
            _ => "Parado",
        }, LunetTokens.Type.Caption, problems > 0 ? LunetTokens.Color.Danger : LunetTokens.Color.TextSecondary);
        status.TextWrapping = TextWrapping.NoWrap;
        bar.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(LunetTokens.Space.Xxs, 0), Children = { _title, status } });
        return new Border
        {
            Child = bar, Background = LunetTokens.Brush(LunetTokens.Color.Surface),
            BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border), BorderThickness = new Thickness(0, 0, 0, 1),
        };
    }

    private Control AssetsView(WorkspaceTab tab)
    {
        var files = ProjectFiles.For(tab, _shell.Workspace.Files());
        var (icon, title, studio) = tab switch
        {
            WorkspaceTab.Art => (LunetIcons.Art, "Arte", "Sprite Studio"),
            WorkspaceTab.Map => (LunetIcons.Map, "Mapa", "Tile Studio"),
            _ => (LunetIcons.Sound, "Som", "Music Studio"),
        };
        var honest = $"O {studio} entra nesta casca na Onda 3, sobre um motor de canvas comum. Os arquivos são os mesmos que o app Android já cria e edita.";
        if (files.Count == 0) return LunetUi.EmptyState(icon, $"Nenhum arquivo de {title.ToLowerInvariant()} neste jogo", honest);
        var list = new StackPanel { Spacing = LunetTokens.Space.Xxs, Margin = new Thickness(LunetTokens.Space.M) };
        list.Children.Add(LunetUi.Title(title));
        list.Children.Add(LunetUi.Secondary(honest));
        foreach (var f in files)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = LunetTokens.Space.S, MinHeight = LunetTokens.MinTouch };
            row.Children.Add(LunetIcons.Create(LunetIcons.File, LunetTokens.Color.TextSecondary));
            var name = LunetUi.Text(f, LunetTokens.Type.Callout); name.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(name);
            list.Children.Add(row);
        }
        return new ScrollViewer { Content = list };
    }

    private static void Detach(Control c)
    {
        switch (c.Parent)
        {
            case Panel p: p.Children.Remove(c); break;
            case Decorator d: d.Child = null; break;
            case ContentControl cc: cc.Content = null; break;
        }
    }
}
