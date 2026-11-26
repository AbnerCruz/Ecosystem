using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
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

    public LunetShell(string projectsRoot)
    {
        Workspace = new WorkspaceSession(projectsRoot);
        LearningProgressPath = Path.Combine(projectsRoot, ".lunet", "learning", "progress.json");
        LibraryPath = Path.Combine(projectsRoot, ".lunet", "library");
        GitAccountPath = Path.Combine(projectsRoot, ".lunet", "git-account.json");
        _settingsPath = Path.Combine(projectsRoot, ".lunet", "settings.json");
        Methods = new MethodCatalog(Path.Combine(projectsRoot, ".lunet", "metodos.json"));
        Game = new GameSession(Workspace);
        _home = new HomeView(this);
        _workspaceView = new WorkspaceView(this);
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
        FontFamily = LunetTokens.UiFont;

        State.Changed += Render;
        Game.StateChanged += () => { _workspaceView.Game.Refresh(); if (State.Level == ShellLevel.Workspace) _workspaceView.Rebuild(); };
        Game.Log += line => Avalonia.Threading.Dispatcher.UIThread.Post(() => _workspaceView.Support.Append(line));
        _timer.Tick += (_, _) => TickGame();
        KeyDown += OnKeyDown;
        Content = _home;
    }

    public WorkspaceSession Workspace { get; }

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

    /// <summary>Plays a game full screen (P4); the Android head runs it in its own process. Null: it runs in the workspace.</summary>
    public Action<string>? PlayGame { get; set; }

    /// <summary>The Biblioteca's folder; the Android head points it at the previous screen's (FilesDir/Library).</summary>
    public string LibraryPath { get; set; }

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
        set { if (_hardwareKeyboard == value) return; _hardwareKeyboard = value; if (Workspace.Project is not null) _workspaceView.Code.Relayout(); }
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

    /// <summary>What the "Mais" panel of a game opens through <see cref="OpenRoute"/> (route, label, icon).</summary>
    public static readonly IReadOnlyList<(string Route, string Label, string Icon)> GameRoutes =
    [
        ("assets", "Recursos e Studios", Ui.LunetIcons.Art), ("notes", "Notas e mapas mentais", Ui.LunetIcons.Notes),
        ("git", "Git", Ui.LunetIcons.Folder), ("apk", "Gerar APK", Ui.LunetIcons.Game), ("editor", "Editor completo", Ui.LunetIcons.File),
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

    /// <summary>The AI settings, key vault and history the head provides (M7); null hides the AI.</summary>
    public AiHost? AiHost { get; set; }

    /// <summary>Opens the command palette (M3) with an optional prefix ("@", ":", "#", "/").</summary>
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
        var before = LiveProblems.Count(p => p.Severity == Lunet.Compiler.DiagnosticSeverity.Error);
        LiveProblems = problems.Where(p => p.Severity != Lunet.Compiler.DiagnosticSeverity.Info).ToArray();
        var after = LiveProblems.Count(p => p.Severity == Lunet.Compiler.DiagnosticSeverity.Error);
        if (State.Panel == SupportPanel.Problems) _workspaceView.Support.Refresh();
        if (before != after) _workspaceView.RefreshTopBar();
    }

    public void OpenProject(string name)
    {
        StopGame();
        Workspace.Open(name);
        Assistant = new Lunet.Editor.EditorAssistant(new Lunet.Compiler.LoadedAssembliesReferenceProvider(
            typeof(Lunet.Game).Assembly, typeof(Lunet.Networking.NetworkSession).Assembly));
        LiveProblems = [];
        var project = Workspace.Project!;
        _ = Assistant.LoadProjectAsync(project.ListFiles().Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Select(f => (f, project.ReadText(f))).ToArray());
        State.OpenProject(name);
        _ = DocsLibrary.WarmUpAsync(); // after the game opens, never on the cold start path
        _workspaceView.Load();
        ApplySettings();
        Render();
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
        return Path.GetFileName(project.Directory);
    }

    /// <summary>Erases a game from the trash for good.</summary>
    public void PurgeProject(string trashId)
    {
        Workspace.Store.Purge(trashId);
        _home.Refresh();
    }

    /// <summary>Back to the Home. Saving and stopping happen in <see cref="Render"/>, so every way back (button, Back, Esc) does it.</summary>
    public void GoHome() => State.GoHome();

    /// <summary>Saves, compiles and runs. On a phone it switches to the Jogo tab; on a wide screen the game is already beside the code.</summary>
    public void RunGame()
    {
        if (Workspace.Project is null) return;
        // Ajustes: "Formatar o código ao rodar" (the same option as the previous screens).
        if (Settings.FormatOnRun && _workspaceView.Code.Surface is CodeSurface code && _workspaceView.Code.OpenPath?.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) == true)
        {
            var formatted = Lunet.Editor.CodeFormatter.Format(code.Text);
            if (formatted != code.Text) code.Replace(0, code.Text.Length, formatted, Math.Min(code.Caret, formatted.Length));
        }
        _workspaceView.SaveAll();
        var (w, h) = Workspace.Project.Manifest.Orientation == "landscape" ? (640, 360) : (360, 640);
        if (Game.Run(w, h))
        {
            if (!State.CodeAndGameSideBySide) State.SelectTab(WorkspaceTab.Game);
            _lastTick = 0;
            _clock.Restart();
            _timer.Start();
            TickGame(1 / 60.0);
        }
        else
        {
            _timer.Stop();
            State.ShowPanel(SupportPanel.Problems);
        }
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
        _inputPane = _top.InputPane;
        _insets = _top.InsetsManager;
        if (_inputPane is not null) _inputPane.StateChanged += OnInputPane;
        if (_insets is not null) _insets.SafeAreaChanged += OnSafeArea;
        _keyboardClosed.Tick += OnKeyboardClosed;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_top is not null) _top.BackRequested -= OnBackRequested;
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
        _workspaceView.SetKeyboardOpen(open || _workspaceView.Code.KeyboardVisible);
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
    public void SaveAll() => _workspaceView.SaveAll();

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
        else if (e.Key == Key.F5 || (e.Key == Key.R && e.KeyModifiers.HasFlag(KeyModifiers.Control))) { RunGame(); e.Handled = true; }
        else if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control)) { _workspaceView.SaveAll(); e.Handled = true; }
        else if (e.Key == Key.F1 || (e.Key == Key.P && e.KeyModifiers.HasFlag(KeyModifiers.Control | KeyModifiers.Shift))) { OpenPalette(); e.Handled = true; }
        else if (e.Key == Key.G && e.KeyModifiers.HasFlag(KeyModifiers.Control)) { OpenPalette(":"); e.Handled = true; }
        else if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control) && State.Level == ShellLevel.Workspace && State.Tab is WorkspaceTab.Code or WorkspaceTab.Notes)
        {
            (State.Tab == WorkspaceTab.Code ? _workspaceView.Code : _workspaceView.Notes).ShowFind();
            e.Handled = true;
        }
    }

    /// <summary>Back closes the Lunet keyboard first (like the system keyboard), then panels, then the workspace.</summary>
    private bool BeforeBack() => (State.Level == ShellLevel.Home && _home.CloseSheet())
        || (State.Level == ShellLevel.Workspace && _workspaceView.CloseStudio())
        || _workspaceView.Code.HideKeyboard() || State.Back();

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
        if (!ReferenceEquals(Content, _workspaceView)) Content = _workspaceView;
        // A panel the person opens takes the place of the keyboard (they are never stacked over the code).
        if (State.Panel != SupportPanel.None) _workspaceView.Code.HideKeyboard();
        _workspaceView.Rebuild();
    }
}
