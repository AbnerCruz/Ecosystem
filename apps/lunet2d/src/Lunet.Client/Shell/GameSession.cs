using Lunet.Host;
using Lunet.Input;

namespace Lunet.Client.Shell;

/// <summary>The state of the game in the workspace, as the speed bar shows it.</summary>
public enum GameState { Stopped, Running, Paused, Faulted, CompileErrors }

/// <summary>
/// The game running inside the workspace (Godot 4.4 style "game embedded in the editor"): run, stop, pause, step one
/// frame and change speed, without leaving the code. No UI dependency: the view only blits <c>Game.Screen</c>.
/// </summary>
public sealed class GameSession : IDisposable
{
    /// <summary>The fixed step used by <see cref="Step"/> and while paused (one 60 Hz frame).</summary>
    public const double StepSeconds = 1 / 60.0;

    /// <summary>The speeds the bar offers.</summary>
    public static readonly IReadOnlyList<double> Speeds = [0.25, 0.5, 1, 2];

    private readonly WorkspaceSession _workspace;
    private RunningGame? _game;

    public GameSession(WorkspaceSession workspace) => _workspace = workspace;

    public GameState State { get; private set; } = GameState.Stopped;
    public double Speed { get; private set; } = 1;
    public RunningGame? Game => _game;
    /// <summary>Compile diagnostics of the last run, or the fault message; empty when everything is fine.</summary>
    public IReadOnlyList<string> Problems { get; private set; } = [];
    public long FramesRendered { get; private set; }
    /// <summary>The last frames, kept while the game runs so the Desempenho panel can open at any time (M6).</summary>
    public FrameStats Frames { get; } = new();

    /// <summary>Lines the game or the session wrote (game log, compile result, faults), for the Console panel.</summary>
    public event Action<string>? Log;
    public event Action? StateChanged;

    /// <summary>Saves nothing by itself: the caller saves the open file first. Compiles and starts the game.</summary>
    public bool Run(int width, int height)
    {
        StopInternal();
        Problems = [];
        RunningGame? game;
        try { game = _workspace.Run(width, height); }
        catch (InvalidOperationException ex)
        {
            Problems = [ex.Message];
            Set(GameState.Faulted);
            Log?.Invoke(ex.Message);
            return false;
        }
        if (game is null)
        {
            Problems = _workspace.LastCompile!.Diagnostics.Select(d => d.ToString()).ToArray();
            Set(GameState.CompileErrors);
            Log?.Invoke($"Compilação com {Problems.Count} problema(s).");
            return false;
        }
        _game = game;
        _game.Host.ProfileFrameTimings = true; // a few Stopwatch reads per frame, for the Desempenho panel
        Frames.Clear();
        _game.Game.Log.Written += (level, message) => Log?.Invoke($"[{level}] {message}");
        FramesRendered = 0;
        Log?.Invoke("Jogo iniciado.");
        Set(GameState.Running);
        return true;
    }

    /// <summary>Advances one display tick. Returns true when a new frame was rendered.</summary>
    public bool Tick(double realSeconds)
    {
        if (_game is null || State != GameState.Running) return false;
        return Advance(Math.Min(0.1, realSeconds) * Speed);
    }

    /// <summary>Pauses a running game, or resumes a paused one.</summary>
    public void TogglePause()
    {
        if (State == GameState.Running) Set(GameState.Paused);
        else if (State == GameState.Paused) Set(GameState.Running);
    }

    /// <summary>Advances exactly one 60 Hz frame; pauses first when running, so the frame is inspectable.</summary>
    public bool Step()
    {
        if (_game is null) return false;
        if (State == GameState.Running) Set(GameState.Paused);
        return State == GameState.Paused && Advance(StepSeconds);
    }

    public void SetSpeed(double speed)
    {
        if (!Speeds.Contains(speed)) throw new ArgumentOutOfRangeException(nameof(speed), speed, "Velocidade fora da barra.");
        Speed = speed;
        StateChanged?.Invoke();
    }

    public void Stop()
    {
        if (_game is null && State == GameState.Stopped) return;
        StopInternal();
        Set(GameState.Stopped);
    }

    public void Pointer(TouchPhase phase, float x, float y) { if (State == GameState.Running) _game?.Pointer(phase, x, y); }
    public void PointerUp() => _game?.PointerUp();
    public void Key(Keys key, bool down) { if (State == GameState.Running) _game?.Key(key, down); }

    private bool Advance(double seconds)
    {
        if (!_game!.Frame(seconds))
        {
            var fault = _game.Host.Fault;
            Problems = [fault?.ToString() ?? "O jogo parou com erro."];
            Log?.Invoke("O jogo parou com erro: " + fault?.Message);
            StopInternal();
            Set(GameState.Faulted);
            return false;
        }
        FramesRendered++;
        var host = _game.Host;
        Frames.Add(new FrameSample(_game.LastFrameMilliseconds, host.LastUpdateCpuMilliseconds, host.LastDrawCpuMilliseconds, host.LastUpdateSteps));
        return true;
    }

    /// <summary>
    /// Draws the current state again without advancing time, so a value changed in the Inspector shows while paused.
    /// Returns true when a frame was rendered. The frame is not counted in <see cref="Frames"/>: it measures nothing.
    /// </summary>
    public bool Redraw()
    {
        if (_game is null || State != GameState.Paused) return false;
        if (_game.Frame(0)) return true;
        Problems = [_game.Host.Fault?.ToString() ?? "O jogo parou com erro."];
        Log?.Invoke("O jogo parou com erro: " + _game.Host.Fault?.Message);
        StopInternal();
        Set(GameState.Faulted);
        return false;
    }

    private void StopInternal()
    {
        _game?.Dispose();
        _game = null;
    }

    private void Set(GameState state)
    {
        State = state;
        StateChanged?.Invoke();
    }

    public void Dispose() => StopInternal();
}
