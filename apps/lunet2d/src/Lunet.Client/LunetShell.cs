using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lunet.Client.Shell;
using Lunet.Client.Ui;
using Lunet.Client.Views;
using Lunet.Host;

namespace Lunet.Client;

/// <summary>
/// The Lunet shell (ADR 0013): one Avalonia control that is the whole app on every head (desktop now, Android next).
/// Three fixed levels — Home, the workspace of a game, and support panels — driven by <see cref="ShellState"/>.
/// </summary>
public sealed class LunetShell : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly System.Diagnostics.Stopwatch _clock = new();
    private readonly HomeView _home;
    private readonly WorkspaceView _workspaceView;
    private double _lastTick;
    private WorkspaceTab _visibleWorkspaceTab;

    public LunetShell(string projectsRoot) : this(new Lunet.Core.ProjectStore(projectsRoot), projectsRoot) { }

    public LunetShell(Lunet.Core.ProjectStore store, string projectsRoot)
    {
        Workspace = new WorkspaceSession(store, projectsRoot);
        RecoveryPath = Path.Combine(projectsRoot, ".lunet", "drafts");
        LearningProgressPath = Path.Combine(projectsRoot, ".lunet", "learning", "progress.json");
        LauncherPreferencesPath = Path.Combine(projectsRoot, ".lunet", "launcher-preferences.json");
        LibraryPath = Path.Combine(projectsRoot, ".lunet", "library");
        GitAccountPath = Path.Combine(projectsRoot, ".lunet", "git-account.json");
        StudioWorkspacePath = Path.Combine(projectsRoot, ".lunet", "studio");
        _settingsPath = Path.Combine(projectsRoot, ".lunet", "settings.json");
        Methods = new MethodCatalog(Path.Combine(projectsRoot, ".lunet", "metodos.json"));
        Game = new GameSession(Workspace);
        _home = new HomeView(this);
        _workspaceView = new WorkspaceView(this);
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
        FontFamily = LunetTokens.UiFont;

        State.Changed += Render;
        Game.StateChanged += () =>
        {
            if (State.Level != ShellLevel.Workspace) return;
            _workspaceView.RefreshTopBar();
            if (State.Panel != SupportPanel.None) _workspaceView.Support.Refresh();
        };
        Game.Log += line => Avalonia.Threading.Dispatcher.UIThread.Post(() => _workspaceView.Support.Append(line));
        _timer.Tick += (_, _) => TickGame();
        // A finger lifted on a text field asks for the keyboard explicitly (after focus has settled on it).
        // Only a tap on the field itself: a key of the code bar or its ⌄ (which closes the keyboard) keep their meaning.
        // A finger that dragged was scrolling: it never asks for the keyboard (owner, 27/11: scrolling the code kept
        // opening it). The press is recorded first, before any control can handle it.
        AddHandler(PointerPressedEvent, (_, e) => TouchTap.Press(e), Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, (_, e) => TouchTap.Release(e), Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, (_, e) => { if (e.Pointer.Captured is null) TouchTap.Cancel(e.Pointer); }, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (e.Pointer.Type == PointerType.Mouse || e.Source is not Visual tapped) return;
            if (TouchTap.Moved(e)) return;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => RequestKeyboardForFocus(tapped));
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        Content = _home;
    }

    /// <summary>
    /// Asks the system keyboard for the focused text field, if any (a TextBox that can be written, or the code editor
    /// with the system keyboard on). Called after a tap; the head turns it into an explicit request, which Android
    /// honours even after Back closed the keyboard.
    /// </summary>
    public bool RequestKeyboardForFocus(Visual? tapped = null)
    {
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not InputElement focused) return false;
        if (tapped is not null && !ReferenceEquals(tapped, focused) && !(focused is Visual f && f.IsVisualAncestorOf(tapped))) return false;
        var field = focused is TextBox { IsReadOnly: false } || focused is AvaloniaEdit.Editing.TextArea { IsReadOnly: false };
        if (!field || !InputMethod.GetIsInputMethodEnabled(focused)) return false;
        KeyboardRequests++;
        SystemKeyboard?.Invoke(true);
        return true;
    }

    /// <summary>How many times a tap asked for the keyboard (tests).</summary>
    public int KeyboardRequests { get; private set; }

    public WorkspaceSession Workspace { get; }
    public string RecoveryPath { get; set; }

    /// <summary>
    /// Pinned and recent games (by GameId) — the single authority shared with the previous screens
    /// (<see cref="Lunet.Core.LauncherPreferences"/>); the Android head points it at FilesDir/launcher-preferences.json.
    /// </summary>
    public string LauncherPreferencesPath { get; set; }

    /// <summary>The launcher preferences, or null when the file is damaged (it is never overwritten then).</summary>
    public Lunet.Core.LauncherPreferences? LauncherPreferences()
    {
        try
        {
            var prefs = new Lunet.Core.LauncherPreferences(LauncherPreferencesPath);
            // G14: a file from an earlier Home keeps pins and the last game by name; the first read in this shell
            // moves them to GameIds (names matching no game are kept), as the previous screens did on start.
            if (_preferencesUpgradedFor != LauncherPreferencesPath)
            {
                prefs.UpgradeProjectReferences(Lunet.Core.LauncherProjectCatalog.Scan(Workspace.Store));
                _preferencesUpgradedFor = LauncherPreferencesPath;
            }
            return prefs;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException) { return null; }
    }

    private string? _preferencesUpgradedFor;

    /// <summary>
    /// Where Aprender keeps the student's declared progress. The Android head points it at the file the previous screens
    /// use (FilesDir/learning/progress.json), so progress carries over; elsewhere it lives beside the games.
    /// </summary>
    public string LearningProgressPath { get; set; }

    /// <summary>The settings file (Ajustes); the Android head points it at the previous screens' settings.json.</summary>
    public string SettingsPath
    {
        get => _settingsPath;
        set { _settingsPath = value; _settings = null; ApplySettings(); }
    }
    private string _settingsPath;
    private Lunet.Core.EditorSettings? _settings;

    /// <summary>The current settings (read once from <see cref="SettingsPath"/>; defaults when the file is missing or broken).</summary>
    public Lunet.Core.EditorSettings Settings
    {
        get
        {
            if (_settings is not null) return _settings;
            try { _settings = new Lunet.Core.SettingsStore(_settingsPath).Load(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { _settings = new Lunet.Core.EditorSettings(); }
            return _settings;
        }
    }

    /// <summary>Saves the settings (verified write) and applies them now. Throws when the file cannot be written.</summary>
    public void SaveSettings(Lunet.Core.EditorSettings settings)
    {
        new Lunet.Core.SettingsStore(_settingsPath).Save(settings);
        _settings = settings;
        ApplySettings();
    }

    /// <summary>Keeps the screen on (true) or lets it sleep; set by the Android head, null elsewhere.</summary>
    public Action<bool>? KeepScreenOn { get; set; }

    /// <summary>Applies the settings to what is open: the code editor and the screen.</summary>
    public void ApplySettings()
    {
        if (_workspaceView?.Code.Surface is CodeSurface code) code.Apply(Settings);
        KeepScreenOn?.Invoke(Settings.KeepScreenOn);
    }

    /// <summary>Plays a WAV (the sound Studio); set by the Android head, null (silent) elsewhere.</summary>
    public Action<byte[]>? PlaySound { get; set; }
    public Action? StopSound { get; set; }
    public Func<bool>? IsSoundPlaying { get; set; }

    /// <summary>Plays a game full screen (P4); the Android head runs it in its own process. Null: it runs in the workspace.</summary>
    public Action<string>? PlayGame { get; set; }

    /// <summary>The Biblioteca's folder; the Android head points it at the previous screen's (FilesDir/Library).</summary>
    public string LibraryPath { get; set; }

    /// <summary>The standalone Studios' folder (Home → Studios: creations without a game); the Android head points it
    /// at the previous screens' FilesDir/StudioWorkspace, so drafts carry over.</summary>
    public string StudioWorkspacePath { get; set; }

    /// <summary>Who signs versions and the token for sending/receiving (the app's private storage; the Android head
    /// points it at the previous screens' FilesDir/git-account.json, so the account carries over).</summary>
    public string GitAccountPath { get; set; } = "";

    /// <summary>Packs and signs a game's APK on the device (P5): the Android head gives the runtime template and the
    /// device key (Android Keystore). Input: the project and its files (game/Game.dll, game/Content/…, game/Assets/…).
    /// Null where an APK cannot be made (desktop): Gerar APK says so.</summary>
    public Func<Lunet.Core.LunetProject, IReadOnlyDictionary<string, byte[]>, byte[]>? PackageGameApk { get; set; }

    /// <summary>How Versões talks to a remote (HTTP by default; tests use a local git).</summary>
    public Func<string, Lunet.Core.GitAccount, Lunet.Git.IGitTransport> GitTransport { get; set; } =
        (url, account) => new Lunet.Git.HttpGitTransport(url, account.Username.Length > 0 ? account.Username : null, account.Token.Length > 0 ? account.Token : null);
    public ShellState State { get; } = new();
    public GameSession Game { get; }
    public HomeView Home => _home;
    public WorkspaceView WorkspaceView => _workspaceView;

    /// <summary>The "Métodos" catalog of the Lunet keyboard (API + the game's members + usage history).</summary>
    public MethodCatalog Methods { get; }

    private bool _hardwareKeyboard;

    /// <summary>A physical keyboard is attached: it has its own symbols, so the code bar steps aside (the head sets it).</summary>
    public bool HardwareKeyboard
    {
        get => _hardwareKeyboard;
        set { if (_hardwareKeyboard == value) return; _hardwareKeyboard = value; if (Workspace.Project is not null) _workspaceView.Code.UpdateKeyboard(); }
    }

    /// <summary>The last message the shell had to tell you (a failed save, a failed open); also in the Console.</summary>
    public string? LastReport { get; private set; }

    /// <summary>
    /// Opens a screen the shell does not serve yet (the Android head routes it to the current screens, over the same
    /// projects). Null on heads that have no such screens: the shell then hides those destinations.
    /// </summary>
    public Action<string, string?>? OpenRoute
    {
        get => _openRoute;
        set { _openRoute = value; _home.Build(); }
    }
    private Action<string, string?>? _openRoute;

    /// <summary>Destinations of the Home served by <see cref="OpenRoute"/> (route, label, icon).</summary>
    public static readonly IReadOnlyList<(string Route, string Label, string Icon)> HomeRoutes =
    [
        ("play", "Jogar", Ui.LunetIcons.Play), ("studios", "Studios", Ui.LunetIcons.Art), ("learn", "Aprender", Ui.LunetIcons.Notes),
        ("library", "Biblioteca", Ui.LunetIcons.Folder), ("explore", "Explorar", Ui.LunetIcons.Map), ("settings", "Ajustes", Ui.LunetIcons.Speed),
    ];

    /// <summary>Back from a routed screen: re-read the games (and the open documents, when not edited here).</summary>
    public void ReturnedFromRoute()
    {
        _settings = null; // the previous screens write the same settings.json
        ApplySettings();
        _home.Refresh();
        if (State.Level == ShellLevel.Workspace && Workspace.Project is not null && State.Project is { } name)
        {
            if (!Workspace.Projects().Contains(name)) { State.GoHome(); return; }
            Workspace.Open(name);
            _workspaceView.ReloadFromDisk();
        }
    }

    /// <summary>The optional paired connection to the Ecosystem on this device (Android); null hides it (NN-003).</summary>
    public Views.EcosystemConnection? EcosystemConnection { get; set; }

    /// <summary>The chosen projects folder (Android: SAF); null hides Ajustes → Pasta de projetos.</summary>
    public Views.ProjectFolderHost? ProjectFolder { get; set; }

    /// <summary>The AI settings, key vault and history the head provides (M7); null hides the AI.</summary>
    public AiHost? AiHost { get; set; }

    /// <summary>Opens the command palette (M3) with an optional prefix ("@", ":", "#", "/").</summary>
    /// <summary>Quick Open: go to any file of the game by name (Ctrl+P, the magnifier in the top bar).</summary>
    public void QuickOpen()
    {
        if (State.Level != ShellLevel.Workspace) return;
        State.ShowPanel(SupportPanel.Commands);
        _workspaceView.Support.Palette.OpenFiles();
    }

    public void OpenPalette(string query = "")
    {
        if (State.Level != ShellLevel.Workspace) return;
        State.ShowPanel(SupportPanel.Commands);
        _workspaceView.Support.Palette.Open(query);
    }

    public void Report(string message)
    {
        LastReport = message;
        _workspaceView.Support.Append(message);
    }

    /// <summary>
    /// The Roslyn analyzer of the open game (errors while typing, completions, signatures — LUNET-536 M1), loaded
    /// with all its .cs files when the game opens. Runs off the UI thread.
    /// </summary>
    public Lunet.Editor.EditorAssistant? Assistant { get; private set; }

    /// <summary>The analyzer's errors and warnings for the open code file (empty while none).</summary>
    public IReadOnlyList<Lunet.Compiler.LunetDiagnostic> LiveProblems { get; private set; } = [];

    /// <summary>Called by the code editor after each analysis; refreshes the top bar and an open Problemas panel.</summary>
    public void SetLiveProblems(IReadOnlyList<Lunet.Compiler.LunetDiagnostic> problems)
    {
        if (LiveProblems.SequenceEqual(problems)) return;
        var previous = LiveProblems;
        LiveProblems = problems.Where(p => p.Severity != Lunet.Compiler.DiagnosticSeverity.Info).ToArray();
        if (State.Panel == SupportPanel.Problems) _workspaceView.Support.Refresh();
        if (previous.Count != LiveProblems.Count
            || previous.Count(p => p.Severity == Lunet.Compiler.DiagnosticSeverity.Error)
                != LiveProblems.Count(p => p.Severity == Lunet.Compiler.DiagnosticSeverity.Error))
            _workspaceView.RefreshTopBar();
    }

    public void OpenProject(string name)
    {
        if (!_workspaceView.SaveAll()) return;
        StopGame();
        try { Workspace.Open(name); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Lunet.Core.ProjectException)
        {
            Report("Não foi possível abrir o jogo: " + ex.Message);
            return;
        }
        try { LauncherPreferences()?.MarkOpenedProject(name, Workspace.Project!.Manifest.GameId); } // recents, as the previous screens
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { }
        Assistant = new Lunet.Editor.EditorAssistant(new Lunet.Compiler.LoadedAssembliesReferenceProvider(
            typeof(Lunet.Game).Assembly, typeof(Lunet.Networking.NetworkSession).Assembly));
        LiveProblems = [];
        var project = Workspace.Project!;
        _ = Assistant.LoadProjectAsync(ReadEditorSources(project));
        State.OpenProject(name);
        _ = DocsLibrary.WarmUpAsync(); // after the game opens, never on the cold start path
        _workspaceView.Load();
        ApplySettings();
    }

    private static IEnumerable<(string Path, string Text)> ReadEditorSources(Lunet.Core.LunetProject project)
    {
        foreach (var file in project.ListFiles())
            if (file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                yield return (file, project.ReadText(file));
    }

    /// <summary>Creates "Jogo-N" (the first free number) from the blank template and opens it.</summary>
    public void CreateProject() => CreateProject(NextProjectName(), Lunet.Core.ProjectTemplate.Blank, "landscape");

    /// <summary>The first free "Jogo-N", the name a new game gets unless the person types another.</summary>
    public string NextProjectName()
    {
        var existing = Workspace.Projects().ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        var n = existing.Count + 1;
        while (existing.Contains($"Jogo-{n}")) n++;
        return $"Jogo-{n}";
    }

    /// <summary>Creates a game with a name, a starting template and an orientation, and opens it.</summary>
    public void CreateProject(string name, Lunet.Core.ProjectTemplate template, string orientation)
    {
        var project = Workspace.Create(name, template, orientation);
        _home.Refresh();
        OpenProject(project.Name);
    }

    /// <summary>Renames a game (its APK identity stays, so an installed copy keeps updating).</summary>
    public string RenameProject(string name, string newName)
    {
        var project = Workspace.Store.Rename(name, newName);
        _home.Refresh();
        return project.Name;
    }

    /// <summary>Copies a game into a new one with its own identity; returns the copy's name.</summary>
    public string DuplicateProject(string name)
    {
        var project = Workspace.Store.Duplicate(name);
        _home.Refresh();
        return project.Name;
    }

    /// <summary>Moves a game to the trash (undoable); returns the trash entry.</summary>
    public Lunet.Core.TrashedProject DeleteProject(string name)
    {
        var trashed = Workspace.Store.MoveToTrash(name);
        _home.Refresh();
        return trashed;
    }

    /// <summary>Brings a game back from the trash; returns its name (a free variant when the name was taken meanwhile).</summary>
    public string RestoreProject(string trashId)
    {
        var project = Workspace.Store.Restore(trashId);
        _home.Refresh();
        return project.Name;
    }

    /// <summary>Writes a game as a ZIP (the same format as the previous screens and the importer). The open game is saved first.</summary>
    public void ExportProjectZip(string name, Stream destination)
    {
        if (State.Project == name) _workspaceView.SaveAll();
        Workspace.Store.Open(name).ExportZip(destination);
    }

    /// <summary>Imports a game from a ZIP; a name already used gets a suffix. Returns the new game's name.</summary>
    public string ImportProjectZip(Stream source)
    {
        var project = Workspace.Store.ImportZip(source);
        Avalonia.Threading.Dispatcher.UIThread.Post(_home.Refresh);
        return Path.GetFileName(project.Files.Location.TrimEnd('/', Path.DirectorySeparatorChar));
    }

    /// <summary>Erases a game from the trash for good.</summary>
    public void PurgeProject(string trashId)
    {
        Workspace.Store.Purge(trashId);
        _home.Refresh();
    }

    /// <summary>Back to the Home. Saving and stopping happen in <see cref="Render"/>, so every way back (button, Back, Esc) does it.</summary>
    public void GoHome() { if (_workspaceView.SaveAll()) State.GoHome(); }

    /// <summary>
    /// Plays a game: full screen on its own (Android: the head's <see cref="PlayGame"/>), else running in the
    /// workspace. The one path for the Jogar grid and the Home's menu.
    /// </summary>
    public void Play(string name)
    {
        if (PlayGame is { } play) { play(name); return; }
        OpenProject(name);
        _ = RunGame();
    }

    public async Task RunGame()
    {
        if (State.Level != ShellLevel.Workspace || Workspace.Project is not { } project || Game.State == GameState.Compiling) return;
        // Ajustes: "Formatar o código ao rodar" (the same option as the previous screens).
        if (Settings.FormatOnRun && _workspaceView.Code.Surface is CodeSurface code && _workspaceView.Code.OpenPath?.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) == true)
        {
            var formatted = Lunet.Editor.CodeFormatter.Format(code.Text);
            if (formatted != code.Text) code.Replace(0, code.Text.Length, formatted, Math.Min(code.Caret, formatted.Length));
        }
        if (!_workspaceView.SaveAll()) return;
        _workspaceView.Code.HideKeyboard();
        SystemKeyboard?.Invoke(false);
        _timer.Stop();
        var (w, h) = project.Manifest.Orientation == "landscape" ? (640, 360) : (360, 640);
        bool ran = Game.GpuFactory is { } factory
            ? await Game.RunOnGpuAsync(factory, project, _workspaceView.OpenPreview)
            : Game.Run(w, h);
        if (State.Level != ShellLevel.Workspace || !ReferenceEquals(Workspace.Project, project)) return;
        if (!ran)
        {
            if (Game.State is GameState.CompileErrors or GameState.Faulted) State.ShowPanel(SupportPanel.Problems);
            return;
        }
        if (Game.Gpu is not null) return;
        _workspaceView.OpenPreview();
        _lastTick = 0;
        _clock.Restart();
        _timer.Start();
        TickGame(1 / 60.0);
    }

    public bool ClosePreview()
    {
        if (State.Level != ShellLevel.Workspace || State.Tab != WorkspaceTab.Game) return false;
        if (Game.State != GameState.Faulted) StopGame();
        return State.ClosePreview();
    }

    public void StopGame()
    {
        _timer.Stop();
        Game.Stop();
    }

    /// <summary>One display tick (the timer calls it; tests call it with a fixed step).</summary>
    public void TickGame(double? seconds = null)
    {
        var now = _clock.Elapsed.TotalSeconds;
        var dt = seconds ?? now - _lastTick;
        _lastTick = now;
        if (Game.Tick(dt)) _workspaceView.Game.Blit();
        else if (Game.State is GameState.Faulted or GameState.Stopped) _timer.Stop();
    }

    private TopLevel? _top;
    private Avalonia.Controls.Platform.IInputPane? _inputPane;
    private Avalonia.Controls.Platform.IInsetsManager? _insets;
    private bool _keyboardShown;
    private readonly DispatcherTimer _keyboardClosed = new() { Interval = KeyboardFit.CloseGrace };

    /// <summary>Shows (true) or hides (false) the system keyboard; set by the Android head, null elsewhere.</summary>
    public Action<bool>? SystemKeyboard { get; set; }

    /// <summary>True while the phone's keyboard is open (after the close grace).</summary>
    public bool KeyboardOpen => _keyboardShown;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _top = TopLevel.GetTopLevel(this);
        if (_top is null) return;
        _top.BackRequested += OnBackRequested;
        _top.KeyDown += OnKeyDown; // on the window: Esc and the shortcuts also work with nothing focused (a sheet just opened)
        _inputPane = _top.InputPane;
        _insets = _top.InsetsManager;
        if (_inputPane is not null) _inputPane.StateChanged += OnInputPane;
        if (_insets is not null) _insets.SafeAreaChanged += OnSafeArea;
        _keyboardClosed.Tick += OnKeyboardClosed;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_top is not null) { _top.BackRequested -= OnBackRequested; _top.KeyDown -= OnKeyDown; }
        if (_inputPane is not null) _inputPane.StateChanged -= OnInputPane;
        if (_insets is not null) _insets.SafeAreaChanged -= OnSafeArea;
        _keyboardClosed.Tick -= OnKeyboardClosed;
        _keyboardClosed.Stop();
        _top = null; _inputPane = null; _insets = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnInputPane(object? sender, Avalonia.Controls.Platform.InputPaneStateEventArgs e)
    {
        if (e.NewState == Avalonia.Controls.Platform.InputPaneState.Open) { _keyboardClosed.Stop(); SetKeyboard(true); }
        else if (!_keyboardClosed.IsEnabled) _keyboardClosed.Start();
    }

    private void OnKeyboardClosed(object? sender, EventArgs e) { _keyboardClosed.Stop(); SetKeyboard(false); }

    private void OnSafeArea(object? sender, Avalonia.Controls.Platform.SafeAreaChangedArgs e) => FitKeyboard();

    /// <summary>The keyboard opened or closed (the platform calls it through the input pane; tests call it directly).</summary>
    public void SetKeyboard(bool open)
    {
        var closed = _keyboardShown && !open;
        _keyboardShown = open;
        if (closed) _workspaceView.Code.SystemKeyboardClosed(); // closed by its own Back/hide key: the code bar goes too
        _workspaceView.SetKeyboardOpen(open || (_workspaceView.CodeVisible && _workspaceView.Code.KeyboardVisible));
        FitKeyboard();
    }

    private void FitKeyboard()
    {
        var height = _top?.ClientSize.Height ?? 0;
        var top = _keyboardShown && _inputPane is { } pane && pane.OccludedRect.Height > 0 ? pane.OccludedRect.Top : double.NaN;
        var covered = KeyboardFit.Cover(height, top, windowShrinks: _insets is { DisplaysEdgeToEdge: false });
        if (Math.Abs(Margin.Bottom - covered) > .5) Margin = new Thickness(0, 0, 0, covered);
    }

    /// <summary>Saves open documents and stops the game (the head calls it when the app goes away).</summary>
    /// <summary>Saves the open documents now (the Android head calls it when the app leaves the screen).</summary>
    public void SaveAll() => TrySaveAll();
    public bool TrySaveAll() => _workspaceView.SaveAll();

    public void CancelStudioAudio()
    {
        var studio = (_home.Sheet as StudiosView)?.Studio ?? _workspaceView.Studio;
        if (studio is MusicStudioView music) music.StopPreview();
        else StopSound?.Invoke();
    }

    public void Shutdown()
    {
        _workspaceView.SaveAll();
        StopGame();
        Game.Dispose();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        State.Resize(e.NewSize.Width);
    }

    private void OnBackRequested(object? sender, RoutedEventArgs e)
    {
        if (BeforeBack()) e.Handled = true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        if (e.Key == Key.Escape && BeforeBack()) e.Handled = true;
        else if (e.Key == Key.F5 || (e.Key == Key.R && e.KeyModifiers.HasFlag(KeyModifiers.Control))) { _ = RunGame(); e.Handled = true; }
        else if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control)) { _workspaceView.SaveAll(); e.Handled = true; }
        else if (e.Key == Key.F1 || (e.Key == Key.P && e.KeyModifiers.HasFlag(KeyModifiers.Control | KeyModifiers.Shift))) { OpenPalette(); e.Handled = true; }
        else if (e.Key == Key.P && e.KeyModifiers == KeyModifiers.Control) { QuickOpen(); e.Handled = true; }
        else if (State.Level == ShellLevel.Workspace && State.Tab is WorkspaceTab.Code or WorkspaceTab.Game
            && ((e.Key == Key.Tab && e.KeyModifiers.HasFlag(KeyModifiers.Control)) || (e.Key is Key.PageDown or Key.PageUp && e.KeyModifiers == KeyModifiers.Control)))
        {
            var back = e.Key == Key.PageUp || e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            _workspaceView.Code.NextTab(back ? -1 : +1);
            e.Handled = true;
        }
        else if (e.Key == Key.W && e.KeyModifiers == KeyModifiers.Control && State.Level == ShellLevel.Workspace && State.Tab is WorkspaceTab.Code or WorkspaceTab.Game
            && _workspaceView.Code.OpenPath is { } closing)
        {
            _workspaceView.Code.CloseTab(closing);
            e.Handled = true;
        }
        else if (e.Key == Key.G && e.KeyModifiers.HasFlag(KeyModifiers.Control)) { OpenPalette(":"); e.Handled = true; }
        else if (e.Key == Key.Left && e.KeyModifiers == KeyModifiers.Alt && State.Level == ShellLevel.Workspace && State.Tab == WorkspaceTab.Code)
        {
            e.Handled = _workspaceView.Code.BackToPosition();
        }
        else if (e.Key == Key.Right && e.KeyModifiers == KeyModifiers.Alt && State.Level == ShellLevel.Workspace && State.Tab == WorkspaceTab.Code)
        {
            e.Handled = _workspaceView.Code.ForwardToPosition();
        }
        else if (e.Key == Key.B && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && State.Level == ShellLevel.Workspace && State.Tab == WorkspaceTab.Code
            && _workspaceView.Code.Surface is Views.CodeSurface block
            && Lunet.Editor.IndentationService.InsertBlock(block.Text, block.SelectionStart, block.SelectionEnd) is var r)
        {
            block.Replace(r.Start, r.Start + r.DeleteLength, r.Insert, r.SelectionEnd);
            block.Select(r.SelectionStart, r.SelectionEnd);
            e.Handled = true;
        }
        else if (e.Key == Key.F && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && State.Level == ShellLevel.Workspace)
        {
            _workspaceView.ShowSearch();
            e.Handled = true;
        }
        else if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control) && State.Level == ShellLevel.Workspace && State.Tab is WorkspaceTab.Code or WorkspaceTab.Notes)
        {
            (State.Tab == WorkspaceTab.Code ? _workspaceView.Code : _workspaceView.Notes).ShowFind();
            e.Handled = true;
        }
    }

    /// <summary>Back closes the Lunet keyboard first (like the system keyboard), then panels, then the workspace.</summary>
    private bool BeforeBack() => (State.Level == ShellLevel.Home && _home.CloseSheet())
        || (State.Level == ShellLevel.Workspace && _workspaceView.CloseStudio())
        || State.ReduceAi() || ClosePreview() || _workspaceView.CancelFileCreation() || _workspaceView.Code.HideKeyboard() || CancelCompilation() || State.Back(TrySaveAll);

    private bool CancelCompilation()
    {
        if (Game.State != GameState.Compiling) return false;
        StopGame();
        return true;
    }

    /// <summary>The system Back, as the heads route it (public for tests).</summary>
    public bool Back() => BeforeBack();

    private void Render()
    {
        if (State.Level == ShellLevel.Home)
        {
            if (ReferenceEquals(Content, _home)) return;
            _workspaceView.SaveAll();
            StopGame();
            _home.Refresh();
            Content = _home;
            return;
        }
        bool leavingPreview = _visibleWorkspaceTab == WorkspaceTab.Game && State.Tab != WorkspaceTab.Game;
        _visibleWorkspaceTab = State.Tab;
        if (leavingPreview && Game.State is GameState.Running or GameState.Paused or GameState.Compiling) StopGame();
        if (!ReferenceEquals(Content, _workspaceView)) Content = _workspaceView;
        // A panel the person opens takes the place of the keyboard (they are never stacked over the code).
        if (State.Panel != SupportPanel.None) _workspaceView.Code.HideKeyboard();
        _workspaceView.Rebuild();
    }
}
