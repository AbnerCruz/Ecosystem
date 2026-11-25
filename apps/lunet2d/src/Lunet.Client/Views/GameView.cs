using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Lunet.Client.Shell;
using Lunet.Client.Ui;
using Lunet.Input;

namespace Lunet.Client.Views;

/// <summary>
/// The "Jogo" view: the running game and its speed bar (run/pause, one-frame step, stop, 0.25×–2×). It lives inside
/// the workspace, so on a wide screen it sits beside the code. Frames are CPU-rendered by the portable host and
/// blitted here; a GPU surface per platform is a later, measured step (ADR 0013).
/// </summary>
public sealed class GameView : UserControl
{
    private readonly LunetShell _shell;
    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private readonly StackPanel _bar = new() { Orientation = Orientation.Horizontal, Spacing = LunetTokens.Space.Xxs, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _status = LunetUi.Text("", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
    private readonly Panel _stage = new();
    private WriteableBitmap? _frame;

    public GameView(LunetShell shell)
    {
        _shell = shell;
        Focusable = true;
        _status.VerticalAlignment = VerticalAlignment.Center;
        _status.TextWrapping = TextWrapping.NoWrap;
        _status.Margin = new Thickness(LunetTokens.Space.Xs, 0);

        // The bar scrolls sideways on a narrow phone instead of clipping its controls.
        var barContent = new StackPanel { Orientation = Orientation.Horizontal, Children = { _bar, _status } };
        var barRow = new ScrollViewer
        {
            Content = barContent, Margin = new Thickness(LunetTokens.Space.Xs, LunetTokens.Space.Xxs),
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };

        _stage.Background = LunetTokens.Brush(LunetTokens.Color.Stage);
        _stage.ClipToBounds = true;
        _stage.Children.Add(_image);
        _image.PointerPressed += (_, e) => { Focus(); Pointer(e, TouchPhase.Pressed); };
        _image.PointerMoved += (_, e) => { if (e.GetCurrentPoint(_image).Properties.IsLeftButtonPressed) Pointer(e, TouchPhase.Moved); };
        _image.PointerReleased += (_, _) => _shell.Game.PointerUp();
        KeyDown += (_, e) => { if (Map(e.Key) is { } k) { _shell.Game.Key(k, true); e.Handled = true; } };
        KeyUp += (_, e) => { if (Map(e.Key) is { } k) { _shell.Game.Key(k, false); e.Handled = true; } };

        var root = new DockPanel();
        DockPanel.SetDock(barRow, Dock.Top);
        root.Children.Add(barRow);
        root.Children.Add(_stage);
        Content = root;
        Background = LunetTokens.Brush(LunetTokens.Color.Surface);

        _shell.Game.StateChanged += Refresh;
        Refresh();
    }

    /// <summary>The idle message over the stage, replaced by the frame once the game runs.</summary>
    private Control Idle() => LunetUi.EmptyState(LunetIcons.Game,
        _shell.Game.State switch
        {
            GameState.CompileErrors => "O jogo não compilou",
            GameState.Faulted => "O jogo parou com erro",
            _ => "Jogo parado",
        },
        _shell.Game.State switch
        {
            GameState.CompileErrors => "Abra Problemas para ver cada erro e a linha onde está.",
            GameState.Faulted => "Veja Problemas e o Console; corrija e rode de novo.",
            _ => "Toque em Rodar: o jogo compila e aparece aqui, sem sair do código.",
        },
        LunetUi.Button("Rodar", ButtonKind.Primary, LunetIcons.Play, () => _shell.RunGame()));

    public void Refresh()
    {
        var game = _shell.Game;
        _bar.Children.Clear();
        var running = game.State is GameState.Running or GameState.Paused;
        if (running)
        {
            _bar.Children.Add(game.State == GameState.Running
                ? LunetUi.IconButton(LunetIcons.Pause, "Pausar", () => game.TogglePause())
                : LunetUi.IconButton(LunetIcons.Play, "Continuar", () => game.TogglePause(), LunetTokens.Color.Primary));
            _bar.Children.Add(LunetUi.IconButton(LunetIcons.Step, "Avançar um quadro", () => { if (game.Step()) Blit(); }));
            _bar.Children.Add(LunetUi.IconButton(LunetIcons.Stop, "Parar", () => _shell.StopGame()));
            // M6: inspect and measure next to the game, one tap away (Godot's Remote tree and Monitors while running).
            _bar.Children.Add(PanelButton(SupportPanel.Inspector, LunetIcons.Inspector, "Inspector"));
            _bar.Children.Add(PanelButton(SupportPanel.Performance, LunetIcons.Pulse, "Desempenho"));
        }
        else _bar.Children.Add(LunetUi.IconButton(LunetIcons.Play, "Rodar", () => _shell.RunGame(), LunetTokens.Color.Primary));
        foreach (var speed in GameSession.Speeds)
        {
            var label = speed + "×";
            var chip = LunetUi.Button(label, speed == game.Speed ? ButtonKind.Primary : ButtonKind.Ghost, null, () => game.SetSpeed(speed));
            chip.MinWidth = LunetTokens.MinTouch; chip.Padding = new Thickness(LunetTokens.Space.Xs, 0);
            Avalonia.Automation.AutomationProperties.SetName(chip, "Velocidade " + label);
            _bar.Children.Add(chip);
        }

        if (!running)
        {
            _frame = null;
            _image.Source = null;
            if (_stage.Children.Count == 1) _stage.Children.Add(Idle());
            else _stage.Children[1] = Idle();
        }
        else if (_stage.Children.Count > 1) _stage.Children.RemoveAt(1);
        UpdateStatus();
    }

    private Button PanelButton(SupportPanel panel, string icon, string label) => LunetUi.IconButton(icon, label,
        () => _shell.State.TogglePanel(panel), _shell.State.Panel == panel ? LunetTokens.Color.Primary : LunetTokens.Color.TextPrimary);

    private void UpdateStatus()
    {
        var game = _shell.Game;
        _status.Text = game.State switch
        {
            GameState.Running => $"{game.Game!.AverageFrameMilliseconds:0.0} ms por quadro · {game.Speed}×",
            GameState.Paused => $"Pausado no quadro {game.FramesRendered}",
            GameState.CompileErrors => $"{game.Problems.Count} problema(s)",
            GameState.Faulted => "Erro no jogo",
            _ => "",
        };
    }

    /// <summary>Copies the last rendered frame into the image (called by the shell after each tick).</summary>
    public unsafe void Blit()
    {
        var game = _shell.Game.Game;
        if (game is null) return;
        var screen = game.Screen;
        if (_frame is null || _frame.PixelSize.Width != screen.Width || _frame.PixelSize.Height != screen.Height)
        {
            _frame = new WriteableBitmap(new PixelSize(screen.Width, screen.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
            _image.Source = _frame;
        }
        using (var buffer = _frame.Lock())
        {
            var span = new Span<byte>((void*)buffer.Address, buffer.RowBytes * screen.Height);
            screen.CopyScreenRgba(span);
        }
        _image.InvalidateVisual();
        if (_shell.Game.FramesRendered % 15 == 0 || _shell.Game.State == GameState.Paused) UpdateStatus();
    }

    private void Pointer(PointerEventArgs e, TouchPhase phase)
    {
        var game = _shell.Game.Game;
        if (game is null || _image.Bounds.Width <= 0) return;
        var p = e.GetPosition(_image);
        double scale = Math.Min(_image.Bounds.Width / game.Screen.Width, _image.Bounds.Height / game.Screen.Height);
        double ox = (_image.Bounds.Width - game.Screen.Width * scale) / 2, oy = (_image.Bounds.Height - game.Screen.Height * scale) / 2;
        _shell.Game.Pointer(phase, (float)((p.X - ox) / scale), (float)((p.Y - oy) / scale));
    }

    /// <summary>Avalonia and Lunet share key names (A–Z, D0–D9, Space, Enter, Escape, Tab, arrows…).</summary>
    internal static Keys? Map(Key key) => key == Key.Back ? Keys.Back
        : Enum.TryParse<Keys>(key.ToString(), out var mapped) && mapped != Keys.None ? mapped : null;
}
