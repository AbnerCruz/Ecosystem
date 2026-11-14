using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Lunet.Host;
using Lunet.Input;

namespace Lunet.Desktop;

/// <summary>
/// The spike shell: projects, files, a code editor, compile/run with a CPU-rendered preview and live numbers
/// (cold start, compile time, frame time). Deliberately small: it measures the shell, it does not replace the Android IDE.
/// </summary>
public sealed class MainWindow : Window
{
    private readonly WorkspaceSession _workspace;
    private readonly ListBox _projects = new();
    private readonly ListBox _files = new();
    private readonly TextBox _editor = new()
    {
        AcceptsReturn = true, AcceptsTab = true, FontFamily = new FontFamily("Cascadia Code,Consolas,DejaVu Sans Mono,monospace"),
        FontSize = 14, TextWrapping = TextWrapping.NoWrap,
    };
    private readonly Image _preview = new() { Stretch = Stretch.Uniform };
    private readonly TextBlock _status = new() { Margin = new Thickness(8, 4) };
    private readonly TextBox _console = new() { IsReadOnly = true, Height = 120, FontFamily = new FontFamily("monospace"), FontSize = 12 };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private RunningGame? _game;
    private WriteableBitmap? _frame;
    private string? _openFile;
    private bool _firstFrameReported;

    public MainWindow(string projectsRoot)
    {
        Title = "Lunet (desktop, prévia)";
        Width = 1280; Height = 800;
        _workspace = new WorkspaceSession(projectsRoot);

        var open = new Button { Content = "Abrir" };
        var create = new Button { Content = "Novo projeto" };
        var save = new Button { Content = "Salvar" };
        var run = new Button { Content = "▶ Rodar" };
        var stop = new Button { Content = "■ Parar" };
        open.Click += (_, _) => OpenSelected();
        create.Click += (_, _) => CreateProject();
        save.Click += (_, _) => SaveFile();
        run.Click += (_, _) => Run();
        stop.Click += (_, _) => Stop();
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(8) };
        foreach (var b in new Control[] { open, create, save, run, stop }) toolbar.Children.Add(b);

        _projects.SelectionChanged += (_, _) => { };
        _projects.DoubleTapped += (_, _) => OpenSelected();
        _files.SelectionChanged += (_, _) => OpenFile(_files.SelectedItem as string);

        var left = new DockPanel { Width = 240 };
        var projectsHeader = new TextBlock { Text = "Projetos", Margin = new Thickness(8, 4), FontWeight = FontWeight.Bold };
        var filesHeader = new TextBlock { Text = "Arquivos", Margin = new Thickness(8, 4), FontWeight = FontWeight.Bold };
        DockPanel.SetDock(projectsHeader, Dock.Top);
        var leftStack = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,2*") };
        leftStack.Children.Add(projectsHeader); Grid.SetRow(projectsHeader, 0);
        leftStack.Children.Add(_projects); Grid.SetRow(_projects, 1);
        leftStack.Children.Add(filesHeader); Grid.SetRow(filesHeader, 2);
        leftStack.Children.Add(_files); Grid.SetRow(_files, 3);
        left.Children.Add(leftStack);

        var previewBorder = new Border { Background = Brushes.Black, Child = _preview, Width = 480 };
        _preview.PointerPressed += (_, e) => Pointer(e, TouchPhase.Pressed);
        _preview.PointerMoved += (_, e) => { if (e.GetCurrentPoint(_preview).Properties.IsLeftButtonPressed) Pointer(e, TouchPhase.Moved); };
        _preview.PointerReleased += (_, _) => _game?.PointerUp();

        var center = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        center.Children.Add(_editor); Grid.SetColumn(_editor, 0);
        center.Children.Add(previewBorder); Grid.SetColumn(previewBorder, 1);

        var root = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top); root.Children.Add(toolbar);
        DockPanel.SetDock(_status, Dock.Bottom); root.Children.Add(_status);
        DockPanel.SetDock(_console, Dock.Bottom); root.Children.Add(_console);
        DockPanel.SetDock(left, Dock.Left); root.Children.Add(left);
        root.Children.Add(center);
        Content = root;

        KeyDown += (_, e) => { if (Map(e.Key) is { } k) _game?.Key(k, true); };
        KeyUp += (_, e) => { if (Map(e.Key) is { } k) _game?.Key(k, false); };
        _timer.Tick += (_, _) => Tick();
        Opened += (_, _) => _status.Text = $"Pronto em {Program.SinceLaunch.ElapsedMilliseconds} ms (partida a frio até a janela) · pasta: {projectsRoot}";
        RefreshProjects();
    }

    private void Log(string line) => _console.Text = (_console.Text ?? "") + line + "\n";

    private void RefreshProjects() => _projects.ItemsSource = _workspace.Projects();

    private void OpenSelected()
    {
        if (_projects.SelectedItem is not string name) return;
        Stop();
        _workspace.Open(name);
        _files.ItemsSource = _workspace.Files();
        OpenFile(_workspace.Project!.Manifest.EntryPoint);
        Title = $"Lunet — {name}";
    }

    private void CreateProject()
    {
        var name = "Jogo " + (_workspace.Projects().Count + 1);
        _workspace.Create(name);
        RefreshProjects();
        _projects.SelectedItem = name;
        OpenSelected();
    }

    private void OpenFile(string? path)
    {
        if (path is null || _workspace.Project is null) return;
        SaveFile();
        _openFile = path;
        _editor.Text = _workspace.Read(path);
    }

    private void SaveFile()
    {
        if (_openFile is null || _workspace.Project is null) return;
        _workspace.Write(_openFile, _editor.Text ?? "");
    }

    private void Run()
    {
        if (_workspace.Project is null) { _status.Text = "Abra ou crie um projeto."; return; }
        SaveFile();
        Stop();
        var started = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var manifest = _workspace.Project.Manifest;
            var (w, h) = manifest.Orientation == "landscape" ? (640, 360) : (360, 640);
            _game = _workspace.Run(w, h);
        }
        catch (InvalidOperationException ex) { Log(ex.Message); return; }
        if (_game is null)
        {
            foreach (var d in _workspace.LastCompile!.Diagnostics) Log(d.ToString());
            _status.Text = "Erros de compilação: veja o console.";
            return;
        }
        Log($"Compilado e iniciado em {started.ElapsedMilliseconds} ms.");
        _game.Game.Log.Written += (level, message) => Avalonia.Threading.Dispatcher.UIThread.Post(() => Log($"[{level}] {message}"));
        _frame = new WriteableBitmap(new PixelSize(_game.Screen.Width, _game.Screen.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        _preview.Source = _frame;
        _firstFrameReported = false;
        _timer.Start();
    }

    private void Stop()
    {
        _timer.Stop();
        _game?.Dispose();
        _game = null;
    }

    private void Tick()
    {
        if (_game is null || _frame is null) return;
        if (!_game.Frame())
        {
            Log("O jogo parou com erro: " + _game.Host.Fault);
            Stop();
            return;
        }
        using (var buffer = _frame.Lock())
        unsafe
        {
            var span = new Span<byte>((void*)buffer.Address, buffer.RowBytes * _game.Screen.Height);
            _game.Screen.CopyScreenRgba(span);
        }
        _preview.InvalidateVisual();
        if (!_firstFrameReported) { _firstFrameReported = true; Log($"Primeiro quadro {Program.SinceLaunch.ElapsedMilliseconds} ms após abrir o Lunet."); }
        _status.Text = $"Quadro: {_game.AverageFrameMilliseconds:0.0} ms de CPU (média de 60) · {_game.Screen.QuadsDrawn} quads";
    }

    private void Pointer(PointerEventArgs e, TouchPhase phase)
    {
        if (_game is null || _preview.Bounds.Width <= 0) return;
        var p = e.GetPosition(_preview);
        // Stretch.Uniform: map the image area back to surface pixels.
        double scale = Math.Min(_preview.Bounds.Width / _game.Screen.Width, _preview.Bounds.Height / _game.Screen.Height);
        double ox = (_preview.Bounds.Width - _game.Screen.Width * scale) / 2, oy = (_preview.Bounds.Height - _game.Screen.Height * scale) / 2;
        _game.Pointer(phase, (float)((p.X - ox) / scale), (float)((p.Y - oy) / scale));
    }

    /// <summary>Avalonia and Lunet share key names (A–Z, D0–D9, Space, Enter, Escape, Tab, arrows…).</summary>
    private static Keys? Map(Key key) => key == Key.Back ? Keys.Back
        : Enum.TryParse<Keys>(key.ToString(), out var mapped) && mapped != Keys.None ? mapped : null;

    /// <summary>Measurement mode: open a project and start it as a person would (buttons call the same methods).</summary>
    internal void OpenAndRun(string project)
    {
        RefreshProjects();
        _projects.SelectedItem = project;
        OpenSelected();
        Run();
    }

    internal void Step() => Tick();

    protected override void OnClosed(EventArgs e)
    {
        SaveFile();
        Stop();
        base.OnClosed(e);
    }
}
