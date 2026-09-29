using Lunet.Content;
using Lunet.Graphics;
using Lunet.Input;

namespace Lunet;

/// <summary>Classe base dos jogos Lunet. Sobrescreva os métodos de ciclo de vida.</summary>
public abstract class Game
{
    private GraphicsDevice? _graphics;
    private InputState? _input;
    private ContentManager? _content;

    public GameConfiguration Configuration { get; } = new();
    public GameLog Log { get; } = new();

    public GraphicsDevice GraphicsDevice => _graphics ?? throw new InvalidOperationException("O jogo ainda não foi iniciado por um host.");
    public InputState Input => _input ?? throw new InvalidOperationException("O jogo ainda não foi iniciado por um host.");

    /// <summary>Conteúdo do projeto (pasta <c>Content/</c>): texturas e textos.</summary>
    public ContentManager Content => _content ?? throw new InvalidOperationException("O jogo ainda não foi iniciado por um host.");

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

    internal void Attach(GraphicsDevice graphics, InputState input, ContentManager content)
    {
        _content = content;
        _graphics = graphics;
        _input = input;
    }

    internal void RunInitialize() => Initialize();
    internal void RunLoadContent() => LoadContent();
    internal void RunUpdate(GameTime time) => Update(time);
    internal void RunDraw(GameTime time) => Draw(time);
    internal void RunUnloadContent() => UnloadContent();
    internal void RunPause() => OnPause();
    internal void RunResume() => OnResume();
}
