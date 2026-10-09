using System.Diagnostics;
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
/// <example>
/// <code>
/// var host = new GameHost(game, backend, source, audioBackend, store, haptics);
/// if (host.Start(1080, 1920)) host.Tick(1.0 / 60);
/// </code>
/// </example>
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
    private bool _profileFrameTimings;

    /// <param name="contentSource">Origem dos arquivos de <c>Content/</c>; sem ela, o jogo não encontra arquivos.</param>
    /// <param name="game">Jogo a executar.</param>
    /// <param name="backend">Backend gráfico (OpenGL ES no Android).</param>
    /// <param name="audio">Backend de áudio; nulo deixa o jogo sem som.</param>
    /// <param name="saveStore">Onde os saves são gravados; nulo usa memória.</param>
    /// <param name="haptics">Vibração do aparelho; nulo desliga.</param>
    public GameHost(Game game, IGraphicsBackend backend, IContentSource? contentSource = null, IAudioBackend? audio = null, ISaveStore? saveStore = null, IHaptics? haptics = null)
    {
        _haptics = haptics ?? new NullHaptics();
        _saveStore = saveStore ?? new MemorySaveStore();
        _audio = audio;
        _contentSource = contentSource ?? new EmptyContentSource();
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    /// <summary>Dispositivo gráfico criado por Start, ou nulo antes disso.</summary>
    public GraphicsDevice? GraphicsDevice { get; private set; }
    /// <summary>Estado de entrada que o host preenche a cada quadro.</summary>
    public InputState Input { get; } = new();
    /// <summary>Verdadeiro se o código do jogo lançou uma exceção e o jogo parou.</summary>
    public bool IsFaulted => Fault is not null;
    /// <summary>Exceção que parou o jogo, ou nulo.</summary>
    public Exception? Fault { get; private set; }
    /// <summary>Verdadeiro enquanto o jogo está pausado.</summary>
    public bool IsPaused => _paused;
    /// <summary>Registro do jogo em execução.</summary>
    public GameLog Log => _game.Log;

    /// <summary>Ativa a medição opt-in dos tempos CPU de Update e Draw; desligado por padrão.</summary>
    /// <remarks>Usa cronômetro monotônico na thread do jogo, sem espera pela GPU. Desativar limpa os últimos valores.</remarks>
    public bool ProfileFrameTimings
    {
        get => _profileFrameTimings;
        set
        {
            if (_profileFrameTimings == value) return;
            _profileFrameTimings = value;
            LastUpdateCpuMilliseconds = 0;
            LastDrawCpuMilliseconds = 0;
            LastUpdateSteps = 0;
        }
    }

    /// <summary>Duração CPU do último grupo de atualizações fixas em milissegundos, incluindo Timers e Audio.Update.</summary>
    /// <remarks>Zero quando pausado, sem passos ou medição desligada. Não inclui Dispatcher nem Draw.</remarks>
    public double LastUpdateCpuMilliseconds { get; private set; }

    /// <summary>Duração CPU da última chamada síncrona de Draw, em milissegundos; não inclui GPU/present.</summary>
    public double LastDrawCpuMilliseconds { get; private set; }

    /// <summary>Número real de passos Update executados no último quadro medido (0 em pausa).</summary>
    public int LastUpdateSteps { get; private set; }

    /// <summary>Inicializa o jogo. Retorna falso se o código do jogo lançou exceção.</summary>
    /// <param name="surfaceWidth">Largura da superfície de desenho, em pixels.</param>
    /// <param name="surfaceHeight">Altura da superfície de desenho, em pixels.</param>
    /// <returns>Verdadeiro se o jogo iniciou sem exceção.</returns>
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
            GraphicsDevice.ViewportScaling = configuration.ViewportScaling;
            GraphicsDevice.Resize(surfaceWidth, surfaceHeight);
            _loop = new FixedTimestepLoop(configuration.UpdatesPerSecond, configuration.MaxFrameSeconds);
            _game.RunLoadContent();
        });
    }

    /// <summary>Densidade da tela e recuos seguros (recortes, cantos, barras) em pixels da superfície.</summary>
    /// <param name="density">Densidade da tela (1 = 160 dpi).</param>
    /// <param name="insetLeft">Recuo seguro à esquerda, em pixels da superfície.</param>
    /// <param name="insetTop">Recuo seguro no topo, em pixels da superfície.</param>
    /// <param name="insetRight">Recuo seguro à direita, em pixels da superfície.</param>
    /// <param name="insetBottom">Recuo seguro embaixo, em pixels da superfície.</param>
    public void SetDisplay(float density, int insetLeft, int insetTop, int insetRight, int insetBottom)
    {
        _display = (density, insetLeft, insetTop, insetRight, insetBottom);
        GraphicsDevice?.SetDisplay(density, insetLeft, insetTop, insetRight, insetBottom);
    }

    /// <summary>Informa o novo tamanho da superfície de desenho.</summary>
    /// <param name="surfaceWidth">Largura em pixels.</param>
    /// <param name="surfaceHeight">Altura em pixels.</param>
    public void Resize(int surfaceWidth, int surfaceHeight) => GraphicsDevice?.Resize(surfaceWidth, surfaceHeight);

    /// <summary>Define os toques do quadro em pixels da superfície; converte para coordenadas virtuais.</summary>
    /// <param name="surfaceTouches">Toques atuais, em pixels da superfície de desenho.</param>
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

    /// <summary>Pausa a simulação e o áudio. O desenho continua.</summary>
    public void Pause()
    {
        if (_paused) return;
        _paused = true;
        _content?.Audio.PauseAll();
        if (!IsFaulted && _started) Guard(_game.RunPause);
    }

    /// <summary>Retoma a simulação e o áudio.</summary>
    public void Resume()
    {
        if (!_paused) return;
        _paused = false;
        _content?.Audio.ResumeAll();
        if (!IsFaulted && _started) Guard(_game.RunResume);
    }

    /// <summary>Um quadro: executa os passos fixos devidos e desenha. Sem alocações no caminho normal.</summary>
    /// <param name="elapsedSeconds">Tempo real passado desde a chamada anterior, em segundos.</param>
    public void Tick(double elapsedSeconds)
    {
        if (!_started || IsFaulted || _loop is null) return;
        _clock += Math.Max(0, elapsedSeconds);
        Input.RecognizeGestures(_clock);
        try
        {
            var profile = _profileFrameTimings;
            if (profile)
            {
                LastUpdateCpuMilliseconds = 0;
                LastDrawCpuMilliseconds = 0;
                LastUpdateSteps = 0;
            }
            _game.RunDispatcher();
            if (!_paused)
            {
                var steps = _loop.Advance(elapsedSeconds);
                var updateStart = profile ? Stopwatch.GetTimestamp() : 0;
                for (var i = 0; i < steps; i++)
                {
                    _game.RunUpdate(_loop.CompleteStep());
                    Input.ClearGestures(); // cada gesto é entregue a um único passo
                }
                if (profile)
                {
                    LastUpdateSteps = steps;
                    if (steps > 0) LastUpdateCpuMilliseconds = Stopwatch.GetElapsedTime(updateStart).TotalMilliseconds;
                }
            }
            var drawStart = profile ? Stopwatch.GetTimestamp() : 0;
            _game.RunDraw(new GameTime(_loop.TotalSeconds, (float)elapsedSeconds, _loop.Interpolation));
            if (profile) LastDrawCpuMilliseconds = Stopwatch.GetElapsedTime(drawStart).TotalMilliseconds;
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
            var profile = _profileFrameTimings;
            if (profile)
            {
                LastUpdateCpuMilliseconds = 0;
                LastDrawCpuMilliseconds = 0;
                LastUpdateSteps = 0;
            }
            var updateStart = profile ? Stopwatch.GetTimestamp() : 0;
            _game.RunUpdate(_loop.CompleteStep());
            if (profile)
            {
                LastUpdateCpuMilliseconds = Stopwatch.GetElapsedTime(updateStart).TotalMilliseconds;
                LastUpdateSteps = 1;
            }
            var drawStart = profile ? Stopwatch.GetTimestamp() : 0;
            _game.RunDraw(new GameTime(_loop.TotalSeconds, (float)_loop.StepSeconds, 0f));
            if (profile) LastDrawCpuMilliseconds = Stopwatch.GetElapsedTime(drawStart).TotalMilliseconds;
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    /// <summary>Encerra o jogo e libera texturas e sons carregados por Content.</summary>
    public void Stop()
    {
        if (!_started) return;
        _started = false;
        ProfileFrameTimings = false;
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
