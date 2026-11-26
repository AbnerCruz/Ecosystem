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
        // Arte, Mapa and Som are one tab on the bar (Material: at most five destinations); their views stay.
        (WorkspaceTab.Art, LunetIcons.Art, "Recursos"),
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
        if (_runFab is not null) _runFab.IsVisible = !open;
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

    private Control? _studio;

    /// <summary>The Studio open over the workspace (Sprite or Tile), or null.</summary>
    public Control? Studio => _studio;

    /// <summary>Opens the Sprite Studio on a sprite of the game, full screen; leaving saves and comes back here (or to <paramref name="reopen"/>).</summary>
    public SpriteStudioView OpenSprite(string path, Action? reopen = null)
    {
        var view = new SpriteStudioView(_shell, path, () => { _studio = null; if (reopen is not null) reopen(); else Rebuild(); });
        _studio = view;
        Rebuild();
        return view;
    }

    /// <summary>Opens the Tile Studio on a map of the game (A2).</summary>
    public TileStudioView OpenMap(string path)
    {
        var view = new TileStudioView(_shell, path, () => { _studio = null; Rebuild(); });
        _studio = view;
        Rebuild();
        return view;
    }

    /// <summary>Opens the sound effects Studio (A4).</summary>
    public SoundStudioView OpenSound(string path)
    {
        var view = new SoundStudioView(_shell, path, () => { _studio = null; Rebuild(); });
        _studio = view;
        Rebuild();
        return view;
    }

    /// <summary>Opens a mind map of the game as an outline (P2).</summary>
    public MindMapView OpenMindMap(string path)
    {
        var view = new MindMapView(_shell, path, () => { _studio = null; Rebuild(); });
        _studio = view;
        Rebuild();
        return view;
    }

    public MindMapView NewMindMap() => OpenMindMap(MindMapView.NewPath(_shell));

    /// <summary>Opens Versões (Git) of the game (P6).</summary>
    public GitView OpenGit()
    {
        var view = new GitView(_shell, () => { _studio = null; Rebuild(); });
        _studio = view;
        Rebuild();
        return view;
    }

    /// <summary>Opens Gerar APK (P5).</summary>
    public ApkView OpenApk()
    {
        var view = new ApkView(_shell, () => { _studio = null; Rebuild(); });
        _studio = view;
        Rebuild();
        return view;
    }

    /// <summary>Opens a data table of the game (A5, Data Studio).</summary>
    public DataView OpenData(string path)
    {
        var view = new DataView(_shell, path, () => { _studio = null; Rebuild(); });
        _studio = view;
        Rebuild();
        return view;
    }

    public DataView NewData(string name = "dados") => OpenData(DataView.Create(_shell, name));

    /// <summary>Opens the game's palette (A5).</summary>
    public PaletteView OpenPalette()
    {
        var view = new PaletteView(_shell, () => { _studio = null; Rebuild(); });
        _studio = view;
        Rebuild();
        return view;
    }

    /// <summary>Opens the particles Studio (A5).</summary>
    public ParticleStudioView OpenParticles(string path)
    {
        var view = new ParticleStudioView(_shell, path, () => { _studio = null; Rebuild(); });
        _studio = view;
        Rebuild();
        return view;
    }

    public ParticleStudioView NewParticles() => OpenParticles(ParticleStudioView.NewPath(_shell));

    /// <summary>Opens the Music Studio (A3).</summary>
    public MusicStudioView OpenMusic(string path)
    {
        var view = new MusicStudioView(_shell, path, () => { _studio = null; Rebuild(); });
        _studio = view;
        Rebuild();
        return view;
    }

    public MusicStudioView NewMusic() => OpenMusic(MusicStudioView.NewPath(_shell));

    /// <summary>A new sound effect.</summary>
    public SoundStudioView NewSound() => OpenSound(SoundStudioView.NewPath(_shell));

    /// <summary>A new map with its editable tileset.</summary>
    public TileStudioView NewMap() => OpenMap(TileStudioView.Create(_shell));

    /// <summary>A new sprite in Content/Textures (its PNG is where the game loads textures from).</summary>
    public SpriteStudioView NewSprite()
    {
        var files = _shell.Workspace.Files();
        var n = 1;
        while (files.Contains($"Content/Textures/sprite-{n}.sprite.json")) n++;
        return OpenSprite($"Content/Textures/sprite-{n}.sprite.json");
    }

    /// <summary>Back with a Studio open: save and leave it. False when none is open.</summary>
    public bool CloseStudio() => _studio switch
    {
        SpriteStudioView sprite => sprite.Close(),
        TileStudioView map => map.Close(),
        SoundStudioView sound => sound.Close(),
        MindMapView mind => mind.Close(),
        MusicStudioView music => music.Close(),
        GitView git => git.Close(),
        ParticleStudioView particles => particles.Close(),
        PaletteView palette => palette.Close(),
        DataView data => data.Back(),
        ApkView apk => apk.Close(),
        _ => _resources?.CloseSheet() ?? false,
    };

    /// <summary>Lays the workspace out for the current state (layout, tab, panel).</summary>
    public void Rebuild()
    {
        if (_studio is not null)
        {
            foreach (var c in new Control[] { _code, _notes, _game, _support }) Detach(c);
            Content = _studio;
            return;
        }
        _tabBar = _sheet = null;
        _runFab = null;
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
            root.Children.Add(WithRunButton(Body(expanded)));
        }
        _support.Refresh();
        Content = root;
    }

    private bool IsSelected(WorkspaceTab tab) => _shell.State.CodeAndGameSideBySide
        ? tab is WorkspaceTab.Code or WorkspaceTab.Game
        : _shell.State.Tab == tab || (tab == WorkspaceTab.Art && _shell.State.Tab is WorkspaceTab.Map or WorkspaceTab.Sound);

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

    /// <summary>
    /// On a phone, Rodar (or Parar) floats bottom-right over the code, where the thumb rests (Material FAB); the game
    /// tab has its own controls at the bottom. Hidden while typing: the code bar's Atalhos has the palette.
    /// </summary>
    private Control WithRunButton(Control body)
    {
        var state = _shell.State;
        if (state.Tab is not (WorkspaceTab.Code or WorkspaceTab.Notes) || _keyboardOpen) return body;
        var running = _shell.Game.State is GameState.Running or GameState.Paused;
        var run = running
            ? LunetUi.Button("Parar", ButtonKind.Secondary, LunetIcons.Stop, () => _shell.StopGame())
            : LunetUi.Button("Rodar", ButtonKind.Primary, LunetIcons.Play, () => _shell.RunGame());
        run.Name = "RunButton";
        run.MinHeight = 56;
        run.CornerRadius = new CornerRadius(LunetTokens.Radius.L);
        run.HorizontalAlignment = HorizontalAlignment.Right;
        run.VerticalAlignment = VerticalAlignment.Bottom;
        run.Margin = new Thickness(LunetTokens.Space.M);
        _runFab = run;
        return new Grid { Children = { body, run } };
    }

    private Button? _runFab;

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
        // Wide screens keep Rodar in the top bar (pointer, both hands); phones float it at the bottom.
        if (_shell.State.Layout == ShellLayout.Expanded)
        {
            var running = _shell.Game.State is GameState.Running or GameState.Paused;
            var run = running
                ? LunetUi.Button("Parar", ButtonKind.Secondary, LunetIcons.Stop, () => _shell.StopGame())
                : LunetUi.Button("Rodar", ButtonKind.Primary, LunetIcons.Play, () => _shell.RunGame());
            run.Name = "RunButton";
            run.Margin = new Thickness(LunetTokens.Space.Xs, 0, 0, 0);
            actions.Children.Add(run);
        }
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

    private ResourcesPage? _resources;

    /// <summary>Recursos: the game's art, maps, sounds, effects, data and fonts in one place (see <see cref="ResourcesPage"/>).</summary>
    public ResourcesPage ResourcesTab => _resources ??= new ResourcesPage(_shell, this);

    private Control AssetsView(WorkspaceTab tab)
    {
        // Rebuilt from disk each time the tab is shown (a Studio may have added files).
        _resources = new ResourcesPage(_shell, this);
        return _resources;
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
