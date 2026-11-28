using System.Diagnostics;
using Android.Opengl;
using Javax.Microedition.Khronos.Opengles;
using Lunet.Audio;
using Lunet.Content;
using Lunet.Input;
using Lunet.Runtime;
using Lunet.Runtime.Profiling;
using Lunet.Storage;
using EGLConfig = Javax.Microedition.Khronos.Egl.EGLConfig;

namespace Lunet.Android.Gles;

/// <summary>Executa um jogo compilado dentro de um GLSurfaceView (Preview).</summary>
internal sealed class PreviewRenderer : Java.Lang.Object, GLSurfaceView.IRenderer
{
    private readonly byte[] _assembly;
    private readonly byte[]? _symbols;
    private readonly Action<LogLevel, string> _log;
    private readonly Action<string>? _onFatal;
    private readonly Action? _onStarted;
    private readonly IContentSource _content;
    private readonly Func<IAudioBackend> _audioFactory;
    private IAudioBackend? _audio;
    private readonly ISaveStore _save;
    private readonly IHaptics _haptics;
    private readonly IGameImagePicker? _imagePicker;
    private GamepadState _gamepad;
    private System.Numerics.Vector3 _gyroscope;
    private volatile bool _appPaused;
    private volatile bool _waitingForWindowFocus;
    private (float Density, int Left, int Top, int Right, int Bottom) _display = (1f, 0, 0, 0, 0);
    private bool _displayDirty = true;
    private readonly System.Collections.Concurrent.ConcurrentQueue<(Keys Key, bool Down)> _keys = new();
    private System.Numerics.Vector3 _accelerometer;
    private readonly object _inputLock = new();
    private readonly Stopwatch _clock = new();
    private readonly TouchFrameBuffer _touches = new();

    private GlesBackend? _backend;
    private LoadedGame? _loaded;
    private GameHost? _host;
    private int _width = 1;
    private int _height = 1;
    private bool _reportedFault;
    private bool _graphicsFault;
    private volatile bool _stepRequested;
    private volatile bool _paused;
    private volatile bool _restartRequested;
    private volatile bool _profilingEnabled;
    private readonly PreviewFrameStatistics _performance = new();

    /// <summary>Janela de medições opcional, lida pela UI sem tocar no estado do Game.</summary>
    public PreviewFrameSnapshot Performance => _performance.Snapshot();

    private long _framesDrawn;
    /// <summary>Frames the game drew on the GL thread so far (read from the UI thread: the shell's status and the CI journey).</summary>
    public long FramesDrawn => Interlocked.Read(ref _framesDrawn);
    public long? MusicUnderruns => (_audio as AndroidAudioBackend)?.MusicUnderruns;
    public bool ProfilerIsPaused => _paused || _appPaused;
    // Leituras atômicas da GL thread; não usar para inferir VRAM total.
    public long EstimatedTextureBytes => _backend?.EstimatedTextureBytes ?? 0;
    public long PeakTextureBytes => _backend?.PeakTextureBytes ?? 0;
    public int LiveTextureCount => _backend?.LiveTextureCount ?? 0;

    public void SetProfilingEnabled(bool enabled)
    {
        _profilingEnabled = enabled;
        _performance.Reset();
    }

    public PreviewRenderer(byte[] assembly, byte[]? symbols, IContentSource content, Func<IAudioBackend> audioFactory, ISaveStore save, IHaptics haptics, Action<LogLevel, string> log, IGameImagePicker? imagePicker = null, Action<string>? onFatal = null, Action? onStarted = null, bool waitForWindowFocus = false)
    {
        _waitingForWindowFocus = waitForWindowFocus;
        _save = save;
        _haptics = haptics;
        _imagePicker = imagePicker;
        _audioFactory = audioFactory;
        _content = content;
        _assembly = assembly;
        _symbols = symbols;
        _log = log;
        _onFatal = onFatal;
        _onStarted = onStarted;
    }

    /// <summary>Instância do jogo em execução (nula antes de iniciar ou após parar); o Inspector lê dela.</summary>
    public Game? CurrentGame => _loaded?.Game;

    public void SetTouches(ReadOnlySpan<TouchPoint> touches)
    {
        lock (_inputLock) _touches.Submit(touches);
    }

    public void SetKey(Keys key, bool down) => _keys.Enqueue((key, down));
    public void SetAccelerometer(System.Numerics.Vector3 value)
    {
        lock (_inputLock) _accelerometer = value;
    }

    /// <summary>Densidade de pixels e recuos seguros (recortes de tela) em pixels da superfície.</summary>
    public void SetDisplay(float density, int left, int top, int right, int bottom)
    {
        lock (_inputLock)
        {
            _display = (density, left, top, right, bottom);
            _displayDirty = true;
        }
    }

    public void SetGamepad(GamepadState state)
    {
        lock (_inputLock) _gamepad = state;
    }

    public void SetGyroscope(System.Numerics.Vector3 value)
    {
        lock (_inputLock) _gyroscope = value;
    }

    /// <summary>App em segundo plano: pausa o jogo (e o áudio) sem mexer no pause escolhido pelo usuário.</summary>
    public void SetAppPaused(bool paused) => _appPaused = paused;

    // Queue before GLSurfaceView.OnPause: no further Draw is guaranteed once GL stops.
    // Only the GL thread accesses GameHost; do not pause the mixer from the UI thread.
    public void PauseForBackground()
    {
        _appPaused = true;
        try { if (_host is { IsPaused: false }) _host.Pause(); }
        catch (Exception ex) { ReportPreviewFault("pausar", ex); }
    }

    public void SetPaused(bool paused) => _paused = paused;
    public void RequestStep() => _stepRequested = true;
    public void RequestRestart() => _restartRequested = true;

    public void OnSurfaceCreated(IGL10? gl, EGLConfig? config)
    {
        _graphicsFault = false;
        try
        {
            // EGL can recreate the context after a surface change or a trip to the background.
            DisposeSession();
            _backend = null;
            _backend = new GlesBackend();
        }
        catch (Exception ex) { ReportPreviewFault("inicializar o OpenGL ES 3.0", ex); }
    }

    public void OnSurfaceChanged(IGL10? gl, int width, int height)
    {
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);
        if (_graphicsFault) return;
        try { _host?.Resize(_width, _height); }
        catch (Exception ex) { ReportPreviewFault("redimensionar", ex); }
    }

    /// <summary>Allows the game to run after Android can route Back to the preview window.</summary>
    public void AllowGameExecution() => _waitingForWindowFocus = false;

    public void OnDrawFrame(IGL10? gl)
    {
        // Complete Android's initial surface redraw without running user code. A game can hang on its very first update.
        if (_waitingForWindowFocus || _backend is null || _graphicsFault) return;
        try { DrawFrame(); }
        catch (Exception ex) { ReportPreviewFault("renderizar", ex); }
    }

    private void DrawFrame()
    {
        if (_restartRequested)
        {
            _restartRequested = false;
            DisposeSession();
        }
        if (_host is null && !StartSession()) return;
        var host = _host!;

        var elapsed = _clock.Elapsed.TotalSeconds;
        _clock.Restart();

        Span<TouchPoint> touches = stackalloc TouchPoint[InputState.MaxTouches];
        int touchCount;
        lock (_inputLock) touchCount = _touches.CopyFrame(touches);
        host.SetSurfaceTouches(touches[..touchCount]);
        while (_keys.TryDequeue(out var key)) host.Input.SetKey(key.Key, key.Down);
        System.Numerics.Vector3 acceleration;
        lock (_inputLock) acceleration = _accelerometer;
        host.Input.SetAccelerometer(acceleration);
        GamepadState pad;
        System.Numerics.Vector3 gyro;
        lock (_inputLock) { pad = _gamepad; gyro = _gyroscope; }
        host.Input.SetGamepad(pad);
        (float Density, int Left, int Top, int Right, int Bottom)? display = null;
        lock (_inputLock)
        {
            if (_displayDirty) { display = _display; _displayDirty = false; }
        }
        if (display is { } d) host.SetDisplay(d.Density, d.Left, d.Top, d.Right, d.Bottom);
        host.Input.SetGyroscope(gyro);

        var wantPaused = _paused || _appPaused;
        if (wantPaused != host.IsPaused)
        {
            if (wantPaused) host.Pause(); else host.Resume();
        }
        bool profile = _profilingEnabled;
        host.ProfileFrameTimings = profile;
        long start = 0, allocationStart = 0;
        if (profile)
        {
            _backend!.BeginProfileFrame();
            start = Stopwatch.GetTimestamp();
            allocationStart = GC.GetAllocatedBytesForCurrentThread();
        }
        var updateBefore = host.CompletedUpdateSteps;
        if (_stepRequested)
        {
            _stepRequested = false;
            host.Step();
        }
        else
        {
            host.Tick(elapsed);
        }
        // Um frame OpenGL em 90/120 Hz pode não ter passo de simulação a 60 Hz.
        // Não consumir Pressed/Released antes de o código do jogo receber um Update.
        if (host.CompletedUpdateSteps != updateBefore)
        {
            lock (_inputLock) _touches.AcknowledgeFrame();
        }
        Interlocked.Increment(ref _framesDrawn);
        if (profile)
        {
            _backend!.EndProfileFrame();
            if (elapsed > 0 && double.IsFinite(elapsed))
            {
                double cpuMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                long bytes = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
                _performance.Record(elapsed, cpuMilliseconds, _backend.FrameDrawCalls,
                    _backend.FrameTriangles, Math.Max(0, bytes),
                    host.LastUpdateCpuMilliseconds, host.LastDrawCpuMilliseconds, host.LastUpdateSteps);
            }
        }

        if (host.IsFaulted && !_reportedFault)
        {
            _reportedFault = true;
            _log(LogLevel.Error, "O jogo parou por uma exceção. Corrija o código e aperte Run ou Restart.");
            _onFatal?.Invoke("O jogo parou por uma exceção. Consulte o diagnóstico no editor.");
        }
    }

    private bool StartSession()
    {
        _reportedFault = false;
        try
        {
            _loaded = GameLoader.Load(_assembly, _symbols);
            _loaded.Game.Log.Written += _log;
            if (_imagePicker is not null) _loaded.Game.Services.Add<IGameImagePicker>(_imagePicker);
            _host = new GameHost(_loaded.Game, _backend!, _content, _audio ??= _audioFactory(), _save, _haptics);
            lock (_inputLock) { _host.SetDisplay(_display.Density, _display.Left, _display.Top, _display.Right, _display.Bottom); _displayDirty = false; }
            _host.Start(_width, _height);
            if (_host.IsFaulted) _onFatal?.Invoke("Falha ao inicializar o jogo.");
            else _onStarted?.Invoke();
            _clock.Restart();
            return true;
        }
        catch (GameLoadException ex)
        {
            _log(LogLevel.Error, ex.Message);
            _onFatal?.Invoke(ex.Message);
            DisposeSession();
            return false;
        }
    }

    private void ReportPreviewFault(string operation, Exception exception)
    {
        if (_graphicsFault) return;
        _graphicsFault = true;
        var message = $"Falha ao {operation} o preview: {exception.Message}";
        _log(LogLevel.Error, message);
        _onFatal?.Invoke(message);
    }

    public void Shutdown()
    {
        try
        {
            DisposeSession();
            _backend?.Dispose();
        }
        catch (Exception ex) { _log(LogLevel.Error, "Falha ao encerrar o preview: " + ex.Message); }
        finally { _backend = null; }
    }

    private void DisposeSession()
    {
        _performance.Reset();
        lock (_inputLock) _touches.Clear();
        try { _host?.Stop(); } catch (Exception ex) { _log(LogLevel.Error, ex.Message); }
        _host = null;
        _loaded?.Dispose();
        _loaded = null;
        _audio?.Dispose();
        _audio = null;
    }
}
