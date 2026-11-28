using Avalonia.Automation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Lunet.Client.Shell;
using Lunet.Client.Ui;
using Lunet.Core;

namespace Lunet.Client.Views;

/// <summary>
/// Level 2 of the shell: one game, with its views as tabs (Código · Jogo · Arte · Mapa · Som · Notas) and the support
/// panels (Console, Problemas) as a bottom sheet on a phone or a side column on a wide screen. No main flow uses a modal
/// dialog. The heavy controls (editor, game) are created once and moved between layouts, so nothing is lost on resize.
/// </summary>
public sealed class WorkspaceView : UserControl
{
    private readonly LunetShell _shell;
    private readonly DocumentEditor _code;
    private readonly DocumentEditor _notes;
    private readonly GameView _game;
    private readonly SupportPanelView _support;
    private readonly TextBlock _title = LunetUi.Title("");
    private Control? _tabBar, _sheet;
    private readonly Border _topHost = new();

    /// <summary>Redraws only the top bar (state, problems), never moving the editor (that would close the keyboard).</summary>
    public void RefreshTopBar() { _topHost.Child = TopBar(); _statusHost.Child = StatusBar(); }
    private bool _keyboardOpen;

    /// <summary>
    /// While the phone's keyboard is open the tab bar and the bottom sheet step aside, so the code, the code bar and
    /// the caret keep the space (changes visibility only: re-parenting the editor would close the keyboard).
    /// </summary>
    public void SetKeyboardOpen(bool open)
    {
        _keyboardOpen = open;
        // A field inside the sheet (the AI's message, the Inspector) is what the keyboard opened for: hiding the sheet
        // would take the focus from it and Android would close the keyboard at once ("teclado não abre na IA").
        _typingInSheet = open && _sheet is not null && FocusedField() is Visual f && _sheet.IsVisualAncestorOf(f);
        if (_tabBar is not null) _tabBar.IsVisible = !open;
        if (_bottomPanel is not null) _bottomPanel.IsVisible = _panelFull || !open;
        if (_sheet is not null) _sheet.IsVisible = !open || _typingInSheet;
        UpdateCursor();
        PlaceRunButton();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (_shell.State.Panel == SupportPanel.Ai && _sheet is Border sheet)
            sheet.Height = CompactAiHeight(e.NewSize.Height);
    }

    private static double CompactAiHeight(double height) =>
        height <= 0 ? 280 : Math.Min(Math.Max(0, height - 54), Math.Max(240, Math.Round(height * 0.5)));

    private bool _typingInSheet;

    /// <summary>
    /// True when the code editor is on screen (Código, or code and game side by side). Only then does the code bar
    /// count as "keyboard open": in Jogo, Recursos or Notas it used to hide the workspace tabs, leaving no way back to
    /// the code but Back (capture 4-jogo, 26/11).
    /// </summary>
    public bool CodeVisible => _shell.State.Tab == WorkspaceTab.Code
        || (_shell.State.Tab == WorkspaceTab.Game && !PreviewFull && _shell.Game.State is GameState.Running or GameState.Paused);
    private (WorkspaceTab, SupportPanel) _place;

    /// <summary>The text field of this workspace that has the focus (code, notes, a field of a panel), or null.</summary>
    private InputElement? FocusedField() =>
        TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is InputElement e
        && (e is TextBox || e is AvaloniaEdit.Editing.TextArea) && e is Visual v && this.IsVisualAncestorOf(v) ? e : null;

    /// <summary>
    /// Gives the focus back to the field that had it before a rebuild moved it (moving clears the focus). Tries now and
    /// once more after layout, when the new tree is attached.
    /// </summary>
    private static void Refocus(InputElement? field)
    {
        if (field is null || field.IsFocused) return;
        if (((Visual)field).IsAttachedToVisualTree() && field.IsEffectivelyVisible && field.Focus()) return;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (((Visual)field).IsAttachedToVisualTree() && field.IsEffectivelyVisible) field.Focus(); },
            Avalonia.Threading.DispatcherPriority.Loaded);
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
        // The status bar's "Ln, Col" follows the caret of the code and of the notes.
        foreach (var editor in new[] { _code, _notes })
            if (editor.Surface is CodeSurface surface) surface.Editor.TextArea.Caret.PositionChanged += (_, _) => UpdateCursor();
    }

    public GameView Game => _game;
    public DocumentEditor Code => _code;
    public DocumentEditor Notes => _notes;
    public SupportPanelView Support => _support;

    /// <summary>Opens the project's documents (entry point in Código, first note in Notas).</summary>
    public void Load()
    {
        _panelFull = false;
        _title.Text = _shell.State.Project ?? "";
        _code.OpenSession(_shell.Workspace.Project!.Manifest.EntryPoint);
        _notes.OpenSession(null);
        Rebuild();
    }

    /// <summary>Reloads the open documents from disk (another screen may have changed them); unsaved edits here win.</summary>
    public void ReloadFromDisk()
    {
        // A file that no longer exists (an AI run undone that had created it, a delete outside) closes its tab instead
        // of being read again (that crashed: "Could not find file", 27/11).
        var files = _shell.Workspace.Project is null ? [] : _shell.Workspace.Files().ToHashSet(StringComparer.Ordinal);
        foreach (var editor in new[] { _code, _notes })
        {
            if (editor.IsDirty) continue;
            if (editor.OpenPath is { } open && !files.Contains(open)) editor.FilesMoved(open, null);
            else editor.Load(editor.OpenPath);
        }
        _explorer?.Fill();
        Rebuild();
    }

    /// <summary>Saves every open document (before running, leaving or closing). Verified write, never memory-only.</summary>
    public bool SaveAll()
    {
        var codeSaved = _code.Save();
        var notesSaved = _notes.Save();
        _code.SaveSession();
        _notes.SaveSession();
        return codeSaved && notesSaved;
    }

    internal async Task<bool> SaveEditorBuffersAsync(LunetProject project)
    {
        if (!await _code.SaveAsync() || !ReferenceEquals(project, _shell.Workspace.Project)) return false;
        return await _notes.SaveAsync() && ReferenceEquals(project, _shell.Workspace.Project);
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
        var view = new MindMapView(_shell, path, () =>
        {
            _studio = null;
            if (_shell.State.Tab == WorkspaceTab.Notes) _notes.RefreshFileTabs();
            Rebuild();
        });
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

    /// <summary>Opens the WAV editor on a game's .wav (cut, volume, loop; saves a copy).</summary>
    public WaveEditorView OpenWave(string path)
    {
        var storage = new Lunet.Core.StudioAssetWorkspace(_shell.Workspace.Project!.Files);
        var audio = Lunet.Studio.WaveAudioDocument.Parse(storage.ReadBytes(path, Lunet.Studio.WaveAudioDocument.MaxSourceBytes));
        var view = new WaveEditorView(_shell, storage, audio, Path.GetFileNameWithoutExtension(path), () => { _studio = null; Rebuild(); });
        _studio = view;
        Rebuild();
        return view;
    }

    /// <summary>Opens Conexões of the game (P8).</summary>
    public ConnectionsView OpenConnections()
    {
        var view = new ConnectionsView(_shell, () => { _studio = null; Rebuild(); });
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
        ConnectionsView connections => connections.Close(),
        WaveEditorView wave => wave.Close(),
        _ => _resources?.CloseSheet() ?? false,
    };

    /// <summary>Lays the workspace out for the current state (layout, tab, panel).</summary>
    public void Rebuild()
    {
        if (_studio is not null)
        {
            foreach (var c in new Control[] { _code, _notes, _game, _support }) Detach(c);
            if (_explorer is not null) Detach(_explorer);
            if (!ReferenceEquals(_studio, _entered)) { LunetUi.Enter(_studio); _entered = _studio; } // §8: a Studio enters once
            Content = _studio;
            return;
        }
        // Same tab and panel (a game state change, turning the phone): the field being written keeps the focus. A new tab or
        // panel is the person going elsewhere, and the keyboard rightly goes.
        var place = (_shell.State.Tab, _shell.State.Panel);
        if (_place.Item1 == WorkspaceTab.Game && place.Tab != WorkspaceTab.Game) _panelFull = false;
        if (place.Tab == WorkspaceTab.Notes && _place.Item1 != WorkspaceTab.Notes) _notes.RefreshFileTabs();
        var focused = place == _place ? FocusedField() : null;
        _keyboardOpen = _shell.KeyboardOpen || (CodeVisible && _code.KeyboardVisible); // per tab, recomputed on each switch
        _place = place;
        _tabBar = _sheet = _bottomPanel = null;
        foreach (var c in new Control[] { _code, _notes, _game, _support }) Detach(c);
        if (_explorer is not null) Detach(_explorer);
        var state = _shell.State;
        var expanded = state.Layout == ShellLayout.Expanded && state.Tab != WorkspaceTab.Game;
        _support.HeaderInPanel = false;

        if (state.AiExpanded)
        {
            _support.Refresh();
            Content = new Border
            {
                Name = "AiExpandedHost",
                Background = LunetTokens.Brush(LunetTokens.Color.Background),
                Child = _support,
            };
            Refocus(focused);
            return;
        }

        // The Preview starts at full workspace height. The same panel can shrink to show the editor underneath.
        var root = new DockPanel();
        if (_topHost.Parent is Panel oldTop) oldTop.Children.Remove(_topHost);
        _topHost.Child = TopBar();
        DockPanel.SetDock(_topHost, Dock.Top);
        root.Children.Add(_topHost);
        if (_statusHost.Parent is Panel oldStatus) oldStatus.Children.Remove(_statusHost);
        _statusHost.Child = StatusBar();
        _statusHost.IsVisible = !_keyboardOpen;
        DockPanel.SetDock(_statusHost, Dock.Bottom);
        root.Children.Add(_statusHost);
        _tabBar = _statusHost;

        if (expanded)
        {
            if (!_explorerHidden)
            {
                var column = new Border
                {
                    Width = 260, Child = Explorer, Background = LunetTokens.Brush(LunetTokens.Color.Surface),
                    BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border), BorderThickness = new Thickness(0, 0, 1, 0), Name = "ExplorerColumn",
                };
                DockPanel.SetDock(column, Dock.Left);
                root.Children.Add(column);
            }
            if (state.Panel is not (SupportPanel.None or SupportPanel.Explorer))
            {
                var column = new Border
                {
                    Width = 380, Child = _support, Background = LunetTokens.Brush(LunetTokens.Color.Surface),
                    BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border), BorderThickness = new Thickness(1, 0, 0, 0),
                };
                DockPanel.SetDock(column, Dock.Right);
                root.Children.Add(column);
            }
            root.Children.Add(Body());
            _support.Refresh();
            Content = root;
            Refocus(focused);
            return;
        }

        var inPanel = state.Panel is SupportPanel.Console or SupportPanel.Problems;
        var sheetPanel = state.Panel is not (SupportPanel.None or SupportPanel.Explorer) && !inPanel;
        if (sheetPanel)
        {
            // AI, Inspector, Documentação…: a sheet over the bottom; the game stays above it when it is running.
            var sheet = new Border
            {
                Height = state.Panel == SupportPanel.Ai ? CompactAiHeight(Bounds.Height)
                    : state.Panel is SupportPanel.Inspector or SupportPanel.Performance or SupportPanel.Docs or SupportPanel.Objectives && Bounds.Height > 0
                        ? Math.Max(280, Math.Round(Bounds.Height * 0.5)) : 280,
                Child = _support, Background = LunetTokens.Brush(LunetTokens.Color.SurfaceRaised),
                CornerRadius = new CornerRadius(LunetTokens.Radius.Xl, LunetTokens.Radius.Xl, 0, 0),
                BorderBrush = LunetTokens.Brush(LunetTokens.Color.BorderStrong), BorderThickness = new Thickness(0, 1, 0, 0),
                BoxShadow = LunetTokens.Low,
            };
            DockPanel.SetDock(sheet, Dock.Bottom);
            _sheet = sheet;
            sheet.IsVisible = !_keyboardOpen || _typingInSheet || ReferenceEquals(focused, _support.Ai.Input);
            root.Children.Add(sheet);
            root.Children.Add(state.Tab == WorkspaceTab.Game ? _game : Body());
        }
        else if (state.Tab == WorkspaceTab.Game || inPanel)
        {
            var panel = BottomPanel(inPanel);
            _bottomPanel = panel;
            panel.IsVisible = _panelFull || !_keyboardOpen;
            if (_panelFull)
            {
                root.Children.Add(panel);
            }
            else
            {
                panel.Height = Bounds.Height > 0 ? Math.Max(240, Math.Round(Bounds.Height * 0.45)) : 320;
                DockPanel.SetDock(panel, Dock.Bottom);
                root.Children.Add(panel);
                root.Children.Add(state.Tab is WorkspaceTab.Notes ? _notes : _code);
            }
        }
        else root.Children.Add(Body());
        _support.Refresh();

        if (state.Panel == SupportPanel.Explorer)
        {
            // The Explorer as a drawer from the left, over the code; a tap outside closes it.
            var scrim = new Border { Background = LunetTokens.Brush(LunetTokens.Color.Scrim), Name = "ExplorerScrim" };
            scrim.PointerPressed += (_, e) => { CancelFileCreation(); _shell.State.ShowPanel(SupportPanel.None); e.Handled = true; };
            var drawer = new Border
            {
                Child = Explorer, Width = Bounds.Width > 0 ? Math.Min(340, Math.Round(Bounds.Width * 0.85)) : 320, HorizontalAlignment = HorizontalAlignment.Left,
                Background = LunetTokens.Brush(LunetTokens.Color.SurfaceRaised), BoxShadow = LunetTokens.Low, Name = "ExplorerDrawer",
            };
            Content = new Grid { Children = { root, LunetUi.Enter(scrim, 0, LunetTokens.Motion.Fast), LunetUi.Enter(drawer, 0, LunetTokens.Motion.Fast) } };
        }
        else Content = root;
        Refocus(focused);
    }

    private Border? _bottomPanel;
    private bool _panelFull;
    public bool PreviewFull => _shell.State.Tab == WorkspaceTab.Game && _panelFull;

    public void OpenPreview()
    {
        _panelFull = true;
        _shell.State.OpenPreview();
    }
    private bool _explorerHidden;
    private readonly Border _statusHost = new() { Name = "StatusBar" };
    private ExplorerView? _explorer;

    /// <summary>The game's file tree (drawer on a phone, side column on a wide screen).</summary>
    public ExplorerView Explorer => _explorer ??= new ExplorerView(_shell, () =>
    {
        if (_shell.State.Layout == ShellLayout.Compact && _shell.State.Panel == SupportPanel.Explorer) _shell.State.ShowPanel(SupportPanel.None);
    });

    public void BeginFileCreation()
    {
        if (_shell.State.Layout == ShellLayout.Compact) _shell.State.ShowPanel(SupportPanel.Explorer);
        else if (_explorerHidden) { _explorerHidden = false; Rebuild(); }
        Explorer.BeginCreate(false);
    }

    public bool CancelFileCreation() => _explorer?.CancelCreate() == true;

    /// <summary>
    /// Search in the whole game (Ctrl+Shift+F, the magnifier): the Explorer comes up in its search, with the selected
    /// word of the code already in the field, like VS Code.
    /// </summary>
    public void ShowSearch()
    {
        string? selected = null;
        if (_code.Surface is CodeSurface code && code.SelectionEnd > code.SelectionStart)
            selected = code.Text[code.SelectionStart..code.SelectionEnd];
        if (_shell.State.Layout == ShellLayout.Compact) { if (_shell.State.Panel != SupportPanel.Explorer) _shell.State.ShowPanel(SupportPanel.Explorer); }
        else if (_explorerHidden) { _explorerHidden = false; Rebuild(); }
        Explorer.ShowSearch(true, selected);
    }

    /// <summary>Shows the Explorer with this file in sight (its folders opened), like VS Code's "Reveal".</summary>
    public void RevealInExplorer(string path)
    {
        if (_shell.State.Layout == ShellLayout.Compact) { if (_shell.State.Panel != SupportPanel.Explorer) _shell.State.ShowPanel(SupportPanel.Explorer); }
        else if (_explorerHidden) { _explorerHidden = false; Rebuild(); }
        Explorer.ShowSearch(false);
        Explorer.Reveal(path);
    }

    /// <summary>True while the bottom panel (Jogo · Console · Problemas) is on screen (tests).</summary>
    public bool BottomPanelVisible => _bottomPanel is { IsVisible: true };

    /// <summary>
    /// Opens a file where it belongs: code and text as a tab of the editor, notes in Notas, mind maps, art, maps, sounds,
    /// music and data in their Studio (the Explorer's tap; the AI's and the problems' jumps go through the editor).
    /// </summary>
    public void OpenFile(string path)
    {
        if (PreviewFull) _shell.ClosePreview();
        var lower = path.ToLowerInvariant();
        if (lower.EndsWith(Lunet.Core.ProjectMindMap.Extension)) { OpenMindMap(path); return; }
        if (lower.EndsWith(".md") && lower.StartsWith(Lunet.Core.ProjectNotes.Folder.ToLowerInvariant() + "/"))
        {
            _shell.State.SelectTab(WorkspaceTab.Notes);
            _notes.Load(path);
            return;
        }
        if (lower.EndsWith(".wav")) { ResourcesTab.EditWave(path); return; }
        var asset = lower.EndsWith(".sprite.json") || lower.EndsWith(".sfx.json") || lower.EndsWith(".song.json") || ProjectFiles.IsMap(path)
            || lower.EndsWith(".png") || lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") || lower.EndsWith(".ogg") || lower.EndsWith(".mp3")
            || lower.EndsWith(".ttf") || lower.EndsWith(".otf") || lower.EndsWith(".particles.json") || lower.StartsWith("content/data/");
        if (asset)
        {
            _shell.State.SelectTab(WorkspaceTab.Art);
            ResourcesTab.Activate(path);
            return;
        }
        if (_shell.State.Tab is not (WorkspaceTab.Code or WorkspaceTab.Game)) _shell.State.SelectTab(WorkspaceTab.Code);
        _code.Load(path);
    }

    internal async Task<bool> OpenCreatedCodeAsync(LunetProject project, string path)
    {
        if (!await _code.LoadCreatedAsync(project, path)) return false;
        if (PreviewFull) _shell.ClosePreview();
        if (_shell.State.Tab is not (WorkspaceTab.Code or WorkspaceTab.Game)) _shell.State.SelectTab(WorkspaceTab.Code);
        return true;
    }

    /// <summary>
    /// After the AI changed files: the code shows the change in the file to keep or undo (Numa-like). On a phone the AI
    /// panel steps down so the code is seen; the IA button brings the conversation back.
    /// </summary>
    public bool ReviewAiChanges(Lunet.Ai.LunetAiCheckpoint checkpoint)
    {
        if (PreviewFull) _shell.ClosePreview();
        if (_shell.State.Tab is not (WorkspaceTab.Code or WorkspaceTab.Game)) _shell.State.SelectTab(WorkspaceTab.Code);
        if (!_code.Review(checkpoint)) return false;
        if (_shell.State.Layout == ShellLayout.Compact && _shell.State.Panel == SupportPanel.Ai) _shell.State.ShowPanel(SupportPanel.None);
        return true;
    }

    /// <summary>A file or folder was renamed (to) or deleted (null): the editors' tabs follow, and the Explorer redraws.</summary>
    public void FilesMoved(string from, string? to)
    {
        _code.FilesMoved(from, to);
        _notes.FilesMoved(from, to);
        _explorer?.Fill();
    }

    private Border BottomPanel(bool showSupport)
    {
        var state = _shell.State;
        var header = new DockPanel { Margin = new Thickness(LunetTokens.Space.Xs, 0) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        var full = LunetUi.IconButton(_panelFull ? LunetIcons.ChevronDown : LunetIcons.Fit, _panelFull ? "Painel menor" : "Painel em tela cheia", () => { _panelFull = !_panelFull; Rebuild(); });
        full.Name = "PanelFull";
        var close = LunetUi.IconButton(LunetIcons.Close, "Fechar painel", () =>
        {
            _panelFull = false;
            if (!_shell.ClosePreview()) state.ShowPanel(SupportPanel.None);
        });
        close.Name = "PanelClose";
        actions.Children.Add(full);
        actions.Children.Add(close);
        DockPanel.SetDock(actions, Dock.Right);
        header.Children.Add(actions);
        var problems = _shell.Game.Problems.Count > 0 ? _shell.Game.Problems.Count : _shell.LiveProblems.Count;
        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (name, label, selected, go) in new (string, string, bool, Action)[]
        {
            ("PanelGame", "Jogo", !showSupport, () =>
            {
                if (state.Tab == WorkspaceTab.Game) state.ShowPanel(SupportPanel.None);
                else OpenPreview();
            }),
            ("PanelConsole", "Console", state.Panel == SupportPanel.Console, () => state.ShowPanel(SupportPanel.Console)),
            ("PanelProblems", problems > 0 ? $"Problemas ({problems})" : "Problemas", state.Panel == SupportPanel.Problems, () => state.ShowPanel(SupportPanel.Problems)),
        })
        {
            var b = LunetUi.Button(label, selected ? ButtonKind.Selected : ButtonKind.Ghost, null, go);
            b.Name = name; b.MinHeight = 40;
            tabs.Children.Add(b);
        }
        header.Children.Add(new ScrollViewer { Content = tabs, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        var body = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        body.Children.Add(header);
        if (showSupport) { _support.HeaderInPanel = true; body.Children.Add(_support); }
        else body.Children.Add(_game);
        return new Border
        {
            Child = body, Name = "BottomPanel", Background = LunetTokens.Brush(LunetTokens.Color.Surface),
            BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border), BorderThickness = new Thickness(0, 1, 0, 0),
        };
    }

    private Control StatusBar()
    {
        var errors = _shell.LiveProblems.Count(p => p.Severity == Lunet.Compiler.DiagnosticSeverity.Error) + _shell.Game.Problems.Count;
        var warnings = _shell.LiveProblems.Count(p => p.Severity == Lunet.Compiler.DiagnosticSeverity.Warning);
        var bar = new DockPanel { Height = LunetTokens.MinTouch, Margin = new Thickness(LunetTokens.Space.Xs, 0) }; // touched with a finger: 48, not a desktop 22
        var ai = LunetUi.Button("IA", _shell.State.Panel == SupportPanel.Ai ? ButtonKind.Selected : ButtonKind.Ghost, LunetIcons.Sparkle, () => _shell.State.TogglePanel(SupportPanel.Ai));
        ai.Name = "AiButton"; ai.MinHeight = LunetTokens.MinTouch; ai.Padding = new Thickness(LunetTokens.Space.S, 0);
        DockPanel.SetDock(ai, Dock.Right);
        bar.Children.Add(ai);
        _cursor.VerticalAlignment = VerticalAlignment.Center;
        _cursor.Margin = new Thickness(LunetTokens.Space.S, 0);
        Detach(_cursor);
        DockPanel.SetDock(_cursor, Dock.Right);
        bar.Children.Add(_cursor);
        UpdateCursor();
        var problems = LunetUi.Button($"⊗ {errors}   ⚠ {warnings}", ButtonKind.Ghost, null, () => _shell.State.ShowPanel(SupportPanel.Problems));
        problems.Name = "StatusProblems"; problems.MinHeight = LunetTokens.MinTouch; problems.Padding = new Thickness(LunetTokens.Space.S, 0);
        problems.Foreground = LunetTokens.Brush(errors > 0 ? LunetTokens.Color.Danger : LunetTokens.Color.TextSecondary);
        AutomationProperties.SetName(problems, $"{errors} erros e {warnings} avisos: abrir Problemas");
        DockPanel.SetDock(problems, Dock.Left);
        bar.Children.Add(problems);
        var running = LunetUi.Text(_shell.Game.State switch
        {
            GameState.Compiling => "Compilando…",
            GameState.Running => "▶ Rodando",
            GameState.Paused => "❚❚ Pausado",
            GameState.CompileErrors => "Não compilou",
            GameState.Faulted => "Parou com erro",
            _ => "",
        }, LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
        running.VerticalAlignment = VerticalAlignment.Center;
        bar.Children.Add(running);
        return new Border
        {
            Child = bar, Background = LunetTokens.Brush(LunetTokens.Color.Surface),
            BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border), BorderThickness = new Thickness(0, 1, 0, 0),
        };
    }

    private readonly TextBlock _cursor = LunetUi.Text("", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);

    /// <summary>Line and column of the caret in the status bar ("Ln 12, Col 8"), like VS Code's right side.</summary>
    public void UpdateCursor()
    {
        var editor = _shell.State.Tab == WorkspaceTab.Notes ? _notes : _code;
        if (editor.Surface is CodeSurface code && editor.OpenPath is not null)
        {
            var caret = code.Editor.TextArea.Caret;
            _cursor.Text = $"Ln {caret.Line}, Col {caret.Column}";
        }
        else _cursor.Text = "";
    }

    /// <summary>The status bar's text of the caret (tests).</summary>
    public string CursorText => _cursor.Text ?? "";

    private bool IsSelected(WorkspaceTab tab) => _shell.State.CodeAndGameSideBySide
        ? tab is WorkspaceTab.Code or WorkspaceTab.Game
        : _shell.State.Tab == tab || (tab == WorkspaceTab.Art && _shell.State.Tab is WorkspaceTab.Map or WorkspaceTab.Sound);

    private Control Body()
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

    /// <summary>Kept for the head and tests: Rodar now lives in the top bar on every screen.</summary>
    public void PlaceRunButton() { }

    /// <summary>The Rodar/Parar button (the top bar's primary action).</summary>
    public Button? RunButton => _runButton;

    private Button? _runButton;

    private Control TopBar()
    {
        var bar = new DockPanel { Height = 56, Margin = new Thickness(LunetTokens.Space.Xxs, 0, LunetTokens.Space.Xs, 0) };
        var back = LunetUi.IconButton(LunetIcons.Back, _shell.State.Tab == WorkspaceTab.Game ? "Voltar ao editor" : "Voltar", () => _shell.Back());
        back.Name = "IdeBack";
        DockPanel.SetDock(back, Dock.Left);
        bar.Children.Add(back);
        var expanded = _shell.State.Layout == ShellLayout.Expanded;
        var explorerOpen = expanded ? !_explorerHidden : _shell.State.Panel == SupportPanel.Explorer;
        var files = LunetUi.IconButton(LunetIcons.Folder, explorerOpen ? "Fechar arquivos" : "Arquivos do jogo", () =>
        {
            if (explorerOpen) CancelFileCreation();
            if (_shell.State.Layout == ShellLayout.Expanded) { _explorerHidden = !_explorerHidden; Rebuild(); }
            else _shell.State.TogglePanel(SupportPanel.Explorer);
        }, explorerOpen ? LunetTokens.Color.Primary : LunetTokens.Color.Info);
        files.Name = "ExplorerButton";
        DockPanel.SetDock(files, Dock.Left);
        bar.Children.Add(files);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = LunetTokens.Space.Xxs, VerticalAlignment = VerticalAlignment.Center };
        var running = _shell.Game.State is GameState.Running or GameState.Paused;
        // A phone has room for the ▶ icon only (the name is for the screen reader); a wide screen shows the label.
        Button run = expanded
            ? (running ? LunetUi.Button("Parar", ButtonKind.Secondary, LunetIcons.Stop, () => _shell.StopGame())
                       : LunetUi.Button("Rodar", ButtonKind.Primary, LunetIcons.Play, () => _ = _shell.RunGame()))
            : LunetUi.IconButton(running ? LunetIcons.Stop : LunetIcons.Play, running ? "Parar" : "Rodar",
                () => { if (running) _shell.StopGame(); else _ = _shell.RunGame(); }, running ? LunetTokens.Color.Danger : LunetTokens.Color.Primary);
        run.Name = "RunButton";
        run.IsEnabled = _shell.Game.State != GameState.Compiling;
        _runButton = run;
        // The magnifier on a wide screen: go to a file by name (Quick Open, Ctrl+P).
        // A phone keeps four buttons (owner, 27/11); there the Explorer's search finds files by name too.
        if (expanded)
        {
            var find = LunetUi.IconButton(LunetIcons.Search, "Ir para arquivo", () => _shell.QuickOpen());
            find.Name = "QuickOpenButton";
            actions.Children.Add(find);
        }
        actions.Children.Add(run);
        var more = LunetUi.IconButton(LunetIcons.More, "Mais: comandos, painéis, Git, APK", () => _shell.State.TogglePanel(SupportPanel.More),
            _shell.State.Panel == SupportPanel.More ? LunetTokens.Color.Primary : LunetTokens.Color.TextPrimary);
        more.Name = "PanelsButton";
        actions.Children.Add(more);
        DockPanel.SetDock(actions, Dock.Right);
        bar.Children.Add(actions);
        _title.Margin = new Thickness(0);
        Detach(_title);
        bar.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(LunetTokens.Space.Xxs, 0), Children = { _title } });
        return new Border
        {
            Child = bar, Background = LunetTokens.Brush(LunetTokens.Color.Surface),
            BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border), BorderThickness = new Thickness(0, 0, 0, 1),
        };
    }

    private ResourcesPage? _resources;
    private Control? _entered;

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
