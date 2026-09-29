using Lunet.Content;
using Lunet.Graphics;
using Lunet.Input;
using Lunet.Storage;

namespace Lunet;

/// <summary>Classe base dos jogos Lunet. Sobrescreva os métodos de ciclo de vida.</summary>
public abstract class Game
{
    private GraphicsDevice? _graphics;
    private InputState? _input;
    private ContentManager? _content;
    private SaveData? _save;
    private IHaptics _haptics = new NullHaptics();

    public GameConfiguration Configuration { get; } = new();
    public GameLog Log { get; } = new();

    /// <summary>Serviços do jogo por tipo (o host registra <c>GraphicsDevice</c>, <c>InputState</c>, <c>ContentManager</c>, <c>SaveData</c>, <c>GameLog</c>, <c>Dispatcher</c> e <c>Timers</c>).</summary>
    public GameServices Services { get; } = new();

    /// <summary>Executa ações na thread do jogo; <c>Post</c> é seguro para qualquer thread.</summary>
    public Dispatcher Dispatcher { get; } = new();

    /// <summary>Vibração do aparelho.</summary>
    public IHaptics Haptics => _haptics;

    /// <summary>Textos traduzidos (atalho para <c>Content.Localization</c>).</summary>
    public Lunet.Content.Localization Localization => Content.Localization;

    /// <summary>Barramentos de volume, música, fades (atalho para <c>Content.Audio</c>).</summary>
    public Audio.AudioMixer Audio => Content.Audio;

    /// <summary>Timers no tempo do jogo (param na pausa).</summary>
    public Timers Timers { get; } = new();

    public GraphicsDevice GraphicsDevice => _graphics ?? throw new InvalidOperationException("O jogo ainda não foi iniciado por um host.");
    public InputState Input => _input ?? throw new InvalidOperationException("O jogo ainda não foi iniciado por um host.");

    /// <summary>Conteúdo do projeto (pasta <c>Content/</c>): texturas e textos.</summary>
    public ContentManager Content => _content ?? throw new InvalidOperationException("O jogo ainda não foi iniciado por um host.");

    /// <summary>Dados salvos do jogo (JSON por chave).</summary>
    public SaveData Save => _save ?? throw new InvalidOperationException("O jogo ainda não foi iniciado por um host.");

    /// <summary>Chamado uma vez, antes de <see cref="LoadContent"/>. Ajuste <see cref="Configuration"/> aqui.</summary>
    protected virtual void Initialize() { }

    /// <summary>Crie texturas e outros recursos aqui.</summary>
    protected virtual void LoadContent() { }

    /// <summary>Passo fixo de simulação.</summary>
    protected virtual void Update(GameTime time) { }

    /// <summary>Desenho; pode rodar mais ou menos vezes que <see cref="Update"/>.</summary>
    protected virtual void Draw(GameTime time) { }

    /// <summary>Liberação de recursos ao encerrar.</summary>
    protected virtual void UnloadContent() { }

    /// <summary>Chamado quando o app vai para segundo plano.</summary>
    protected virtual void OnPause() { }

    /// <summary>Chamado quando o app volta ao primeiro plano.</summary>
    protected virtual void OnResume() { }

    internal void Attach(GraphicsDevice graphics, InputState input, ContentManager content, SaveData save, IHaptics haptics)
    {
        _haptics = haptics;
        Services.Add(haptics);
        _save = save;
        Services.Add(graphics);
        Services.Add(input);
        Services.Add(content);
        Services.Add(save);
        Services.Add(Log);
        Services.Add(Dispatcher);
        Services.Add(Timers);
        _content = content;
        _graphics = graphics;
        _input = input;
    }

    internal void RunInitialize() => Initialize();
    internal void RunLoadContent() => LoadContent();
    internal void RunDispatcher() => Dispatcher.RunPending();
    internal void RunUpdate(GameTime time)
    {
        Timers.Update(time.DeltaSeconds);
        Content.Audio.Update(time.DeltaSeconds);
        Update(time);
    }
    internal void RunDraw(GameTime time) => Draw(time);
    internal void RunUnloadContent() => UnloadContent();
    internal void RunPause() => OnPause();
    internal void RunResume() => OnResume();
}
