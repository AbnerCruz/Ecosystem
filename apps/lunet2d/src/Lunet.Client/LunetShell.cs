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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is { } top) top.BackRequested += OnBackRequested;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is { } top) top.BackRequested -= OnBackRequested;
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>Saves open documents and stops the game (the head calls it when the app goes away).</summary>
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

    private bool BeforeBack() => State.Back();

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
