using System.Diagnostics;
using Android.Opengl;
using Javax.Microedition.Khronos.Opengles;
using Lunet.Audio;
using Lunet.Content;
using Lunet.Input;
using Lunet.Runtime;
using Lunet.Storage;
using EGLConfig = Javax.Microedition.Khronos.Egl.EGLConfig;

namespace Lunet.Android.Gles;

/// <summary>Executa um jogo compilado dentro de um GLSurfaceView (Preview).</summary>
internal sealed class PreviewRenderer : Java.Lang.Object, GLSurfaceView.IRenderer
{
    private readonly byte[] _assembly;
    private readonly byte[]? _symbols;
    private readonly Action<LogLevel, string> _log;
    private readonly IContentSource _content;
    private readonly Func<IAudioBackend> _audioFactory;
    private IAudioBackend? _audio;
    private readonly ISaveStore _save;
    private readonly IHaptics _haptics;
    private GamepadState _gamepad;
    private System.Numerics.Vector3 _gyroscope;
    private volatile bool _appPaused;
    private (float Density, int Left, int Top, int Right, int Bottom) _display = (1f, 0, 0, 0, 0);
    private bool _displayDirty = true;
    private readonly System.Collections.Concurrent.ConcurrentQueue<(Keys Key, bool Down)> _keys = new();
    private System.Numerics.Vector3 _accelerometer;
    private readonly object _inputLock = new();
    private readonly Stopwatch _clock = new();
    private TouchPoint[] _touches = [];

    private GlesBackend? _backend;
    private LoadedGame? _loaded;
    private GameHost? _host;
    private int _width = 1;
    private int _height = 1;
    private bool _reportedFault;
    private volatile bool _stepRequested;
    private volatile bool _paused;
    private volatile bool _restartRequested;

    public PreviewRenderer(byte[] assembly, byte[]? symbols, IContentSource content, Func<IAudioBackend> audioFactory, ISaveStore save, IHaptics haptics, Action<LogLevel, string> log)
    {
        _save = save;
        _haptics = haptics;
        _audioFactory = audioFactory;
        _content = content;
        _assembly = assembly;
        _symbols = symbols;
        _log = log;
    }

    /// <summary>Instância do jogo em execução (nula antes de iniciar ou após parar); o Inspector lê dela.</summary>
    public Game? CurrentGame => _loaded?.Game;

    public void SetTouches(TouchPoint[] touches)
    {
        lock (_inputLock) _touches = touches;
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

    public void SetPaused(bool paused) => _paused = paused;
    public void RequestStep() => _stepRequested = true;
    public void RequestRestart() => _restartRequested = true;

    public void OnSurfaceCreated(IGL10? gl, EGLConfig? config)
    {
        // O contexto GL é novo (primeira vez ou após voltar do segundo plano): recursos antigos são inválidos.
        DisposeSession();
        try
        {
            _backend = new GlesBackend();
        }
        catch (Exception ex)
        {
            _log(LogLevel.Error, "OpenGL ES 3.0 indisponível: " + ex.Message);
        }
    }

    public void OnSurfaceChanged(IGL10? gl, int width, int height)
    {
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);
        _host?.Resize(_width, _height);
    }

    public void OnDrawFrame(IGL10? gl)
    {
        if (_backend is null) return;
        if (_restartRequested)
        {
            _restartRequested = false;
            DisposeSession();
        }
        if (_host is null && !StartSession()) return;
        var host = _host!;

        var elapsed = _clock.Elapsed.TotalSeconds;
        _clock.Restart();

        TouchPoint[] touches;
        lock (_inputLock) touches = _touches;
        host.SetSurfaceTouches(touches);
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
        if (_stepRequested)
        {
            _stepRequested = false;
            host.Step();
        }
        else
        {
            host.Tick(elapsed);
        }

        if (host.IsFaulted && !_reportedFault)
        {
            _reportedFault = true;
            _log(LogLevel.Error, "O jogo parou por uma exceção. Corrija o código e aperte Run ou Restart.");
        }
    }

    private bool StartSession()
    {
        _reportedFault = false;
        try
        {
            _loaded = GameLoader.Load(_assembly, _symbols);
            _loaded.Game.Log.Written += _log;
            _host = new GameHost(_loaded.Game, _backend!, _content, _audio ??= _audioFactory(), _save, _haptics);
            lock (_inputLock) { _host.SetDisplay(_display.Density, _display.Left, _display.Top, _display.Right, _display.Bottom); _displayDirty = false; }
            _host.Start(_width, _height);
            _clock.Restart();
            return true;
        }
        catch (GameLoadException ex)
        {
            _log(LogLevel.Error, ex.Message);
            DisposeSession();
            return false;
        }
    }

    public void Shutdown()
    {
        DisposeSession();
        _backend?.Dispose();
        _backend = null;
    }

    private void DisposeSession()
    {
        try { _host?.Stop(); } catch (Exception ex) { _log(LogLevel.Error, ex.Message); }
        _host = null;
        _loaded?.Dispose();
        _loaded = null;
        _audio?.Dispose();
        _audio = null;
    }
}
