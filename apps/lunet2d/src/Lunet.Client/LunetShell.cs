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
    public ShellState State { get; } = new();
    public GameSession Game { get; }
    public HomeView Home => _home;
    public WorkspaceView WorkspaceView => _workspaceView;

    /// <summary>The "Métodos" catalog of the Lunet keyboard (API + the game's members + usage history).</summary>
    public MethodCatalog Methods { get; }

    private bool _hardwareKeyboard, _preferSystemKeyboard;

    /// <summary>A physical keyboard is attached: it types directly and the Lunet keyboard steps aside (the head sets it).</summary>
    public bool HardwareKeyboard
    {
        get => _hardwareKeyboard;
        set { if (_hardwareKeyboard == value) return; _hardwareKeyboard = value; if (Workspace.Project is not null) _workspaceView.Code.Relayout(); }
    }

    /// <summary>
    /// The person chose the system keyboard (🌐), or a screen reader is on (TalkBack works best with the system
    /// keyboard — study 25/11 §3); "Teclado Lunet" brings ours back.
    /// </summary>
    public bool PreferSystemKeyboard
    {
        get => _preferSystemKeyboard;
        set => _preferSystemKeyboard = value;
    }

    /// <summary>The last message the shell had to tell you (a failed save, a failed open); also in the Console.</summary>
    public string? LastReport { get; private set; }

    public void Report(string message)
    {
        LastReport = message;
        _workspaceView.Support.Append(message);
    }

    public void OpenProject(string name)
    {
        StopGame();
        Workspace.Open(name);
        State.OpenProject(name);
        _workspaceView.Load();
        Render();
    }

    /// <summary>Creates "Jogo-N" (the first free number) from the blank template and opens it.</summary>
    public void CreateProject()
    {
        var existing = Workspace.Projects().ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        var n = existing.Count + 1;
        while (existing.Contains($"Jogo-{n}")) n++;
        Workspace.Create($"Jogo-{n}");
        _home.Refresh();
        OpenProject($"Jogo-{n}");
    }

    /// <summary>Back to the Home. Saving and stopping happen in <see cref="Render"/>, so every way back (button, Back, Esc) does it.</summary>
    public void GoHome() => State.GoHome();

    /// <summary>Saves, compiles and runs. On a phone it switches to the Jogo tab; on a wide screen the game is already beside the code.</summary>
    public void RunGame()
    {
        if (Workspace.Project is null) return;
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
        _keyboardShown = open;
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
    }

    /// <summary>Back closes the Lunet keyboard first (like the system keyboard), then panels, then the workspace.</summary>
    private bool BeforeBack() => _workspaceView.Code.HideKeyboard() || State.Back();

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
        _workspaceView.Rebuild();
    }
}
