using Lunet.Audio;
using Lunet.Content;
using Lunet.Graphics;
using Lunet.Input;
using Lunet.Storage;

namespace Lunet;

/// <summary>Fachada contextual experimental, disponível somente em callbacks síncronos do host.</summary>
/// <remarks>Não flui para workers ou continuações assíncronas. Use as APIs explícitas para dependências retidas.
/// Graphics e Content exigem autorização gráfica na fase corrente.</remarks>
/// <example><code>var current = Context.Input;</code></example>
public static class Context
{
    [ThreadStatic] private static Frame _current;
    internal readonly record struct Frame(GameHost? Host, Game? Game, int Generation, bool GraphicsAllowed);

    private static Game Current(bool graphics = false)
    {
        var frame = _current;
        if (frame.Host is not { } host || frame.Game is not { } game || !host.IsContextActive(frame.Generation))
            throw new InvalidOperationException("Lunet.Context exige um callback síncrono de um host ativo.");
        if (graphics && !frame.GraphicsAllowed)
            throw new InvalidOperationException("Lunet.Context: acesso gráfico não autorizado nesta fase ou thread.");
        return game;
    }

    /// <summary>Entrada real do jogo corrente.</summary>
    public static InputState Input => Current().Input;
    /// <summary>Conteúdo do jogo corrente; exige autorização gráfica.</summary>
    public static ContentManager Content => Current(graphics: true).Content;
    /// <summary>Dispositivo real do jogo corrente; exige autorização gráfica.</summary>
    public static GraphicsDevice Graphics => Current(graphics: true).GraphicsDevice;
    /// <summary>Mixer real do jogo corrente.</summary>
    public static AudioMixer Audio => Current().Audio;
    /// <summary>Dados salvos do jogo corrente.</summary>
    public static SaveData Save => Current().Save;
    /// <summary>Serviços extensíveis do jogo corrente.</summary>
    public static GameServices Services => Current().Services;
    /// <summary>Timers do jogo corrente.</summary>
    public static Timers Timers => Current().Timers;
    /// <summary>Registro do jogo corrente.</summary>
    public static GameLog Log => Current().Log;
    /// <summary>Dispatcher do jogo corrente; capture-o explicitamente antes de postar de um worker.</summary>
    public static Dispatcher Dispatcher => Current().Dispatcher;

    internal static Scope Enter(GameHost host, Game game, bool graphics)
    {
        var previous = _current;
        _current = new Frame(host, game, host.ContextGeneration, graphics && host.ContextGraphicsAllowed);
        return new Scope(previous);
    }

    internal readonly struct Scope : IDisposable
    {
        private readonly Frame _previous;
        internal Scope(Frame previous) => _previous = previous;
        public void Dispose() => _current = _previous;
    }
}
