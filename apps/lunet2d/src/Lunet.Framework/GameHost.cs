using Lunet.Audio;
using Lunet.Content;
using Lunet.Graphics;
using Lunet.Input;
using Lunet.Storage;

namespace Lunet;

/// <summary>
/// Executa um <see cref="Game"/> sobre um <see cref="IGraphicsBackend"/>: ciclo de vida, passo fixo e desenho.
/// Falhas no código do jogo são capturadas: o host passa a <see cref="IsFaulted"/> e para de rodar o jogo.
/// </summary>
public sealed class GameHost
{
    private readonly Game _game;
    private readonly IGraphicsBackend _backend;
    private readonly IContentSource _contentSource;
    private readonly IAudioBackend? _audio;
    private readonly ISaveStore _saveStore;
    private readonly IHaptics _haptics;
    private ContentManager? _content;
    private FixedTimestepLoop? _loop;
    private double _clock;
    private (float Density, int Left, int Top, int Right, int Bottom) _display = (1f, 0, 0, 0, 0);
    private bool _paused;
    private bool _started;

    /// <param name="contentSource">Origem dos arquivos de <c>Content/</c>; sem ela, o jogo não encontra arquivos.</param>
    public GameHost(Game game, IGraphicsBackend backend, IContentSource? contentSource = null, IAudioBackend? audio = null, ISaveStore? saveStore = null, IHaptics? haptics = null)
    {
        _haptics = haptics ?? new NullHaptics();
        _saveStore = saveStore ?? new MemorySaveStore();
        _audio = audio;
        _contentSource = contentSource ?? new EmptyContentSource();
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public GraphicsDevice? GraphicsDevice { get; private set; }
    public InputState Input { get; } = new();
    public bool IsFaulted => Fault is not null;
    public Exception? Fault { get; private set; }
    public bool IsPaused => _paused;
    public GameLog Log => _game.Log;

    /// <summary>Inicializa o jogo. Retorna falso se o código do jogo lançou exceção.</summary>
    public bool Start(int surfaceWidth, int surfaceHeight)
    {
        if (_started) throw new InvalidOperationException("O host já foi iniciado.");
        _started = true;
        return Guard(() =>
        {
            var configuration = _game.Configuration;
            var device = new GraphicsDevice(_backend, configuration.VirtualWidth, configuration.VirtualHeight);
            _content = new ContentManager(_contentSource, device, _audio);
            _game.Attach(device, Input, _content, new SaveData(_saveStore), _haptics);
            GraphicsDevice = device;
            GraphicsDevice.SetDisplay(_display.Density, _display.Left, _display.Top, _display.Right, _display.Bottom);
            GraphicsDevice.Resize(surfaceWidth, surfaceHeight);
            _game.RunInitialize();
            configuration.Validate();
            GraphicsDevice.SetVirtualResolution(configuration.VirtualWidth, configuration.VirtualHeight);
            GraphicsDevice.Resize(surfaceWidth, surfaceHeight);
            _loop = new FixedTimestepLoop(configuration.UpdatesPerSecond, configuration.MaxFrameSeconds);
            _game.RunLoadContent();
        });
    }

    /// <summary>Densidade da tela e recuos seguros (recortes, cantos, barras) em pixels da superfície.</summary>
    public void SetDisplay(float density, int insetLeft, int insetTop, int insetRight, int insetBottom)
    {
        _display = (density, insetLeft, insetTop, insetRight, insetBottom);
        GraphicsDevice?.SetDisplay(density, insetLeft, insetTop, insetRight, insetBottom);
    }

    public void Resize(int surfaceWidth, int surfaceHeight) => GraphicsDevice?.Resize(surfaceWidth, surfaceHeight);

    /// <summary>Define os toques do quadro em pixels da superfície; converte para coordenadas virtuais.</summary>
    public void SetSurfaceTouches(ReadOnlySpan<TouchPoint> surfaceTouches)
    {
        if (GraphicsDevice is null) return;
        Span<TouchPoint> virtualTouches = stackalloc TouchPoint[InputState.MaxTouches];
        var count = Math.Min(surfaceTouches.Length, InputState.MaxTouches);
        for (var i = 0; i < count; i++)
        {
            var t = surfaceTouches[i];
            virtualTouches[i] = new TouchPoint(t.Id, t.Phase, GraphicsDevice.SurfaceToVirtual(t.Position));
        }
        Input.SetTouches(virtualTouches[..count]);
    }

    public void Pause()
    {
        if (_paused) return;
        _paused = true;
        _content?.Audio.PauseAll();
        if (!IsFaulted && _started) Guard(_game.RunPause);
    }

    public void Resume()
    {
        if (!_paused) return;
        _paused = false;
        _content?.Audio.ResumeAll();
        if (!IsFaulted && _started) Guard(_game.RunResume);
    }

    /// <summary>Um quadro: executa os passos fixos devidos e desenha. Sem alocações no caminho normal.</summary>
    public void Tick(double elapsedSeconds)
    {
        if (!_started || IsFaulted || _loop is null) return;
        _clock += Math.Max(0, elapsedSeconds);
        Input.RecognizeGestures(_clock);
        try
        {
            _game.RunDispatcher();
            if (!_paused)
            {
                var steps = _loop.Advance(elapsedSeconds);
                for (var i = 0; i < steps; i++)
                {
                    _game.RunUpdate(_loop.CompleteStep());
                    Input.ClearGestures(); // cada gesto é entregue a um único passo
                }
            }
            _game.RunDraw(new GameTime(_loop.TotalSeconds, (float)elapsedSeconds, _loop.Interpolation));
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    /// <summary>Executa um único passo de atualização com o jogo pausado (depuração).</summary>
    public void Step()
    {
        if (!_started || IsFaulted || _loop is null) return;
        try
        {
            _game.RunUpdate(_loop.CompleteStep());
            _game.RunDraw(new GameTime(_loop.TotalSeconds, (float)_loop.StepSeconds, 0f));
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    public void Stop()
    {
        if (!_started) return;
        _started = false;
        Guard(_game.RunUnloadContent);
        _content?.Dispose();
        _content = null;
        GraphicsDevice?.ReleaseResources();
    }

    private bool Guard(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            Fail(ex);
            return false;
        }
    }

    private void Fail(Exception ex)
    {
        Fault = ex;
        _game.Log.Error($"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
    }
}

internal sealed class EmptyContentSource : IContentSource
{
    public bool Exists(string path) => false;
    public Stream Open(string path) => throw new FileNotFoundException($"Arquivo de conteúdo não encontrado: {path}");
}
