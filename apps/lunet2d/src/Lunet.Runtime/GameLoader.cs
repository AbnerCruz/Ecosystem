using System.Reflection;
using System.Runtime.Loader;

namespace Lunet.Runtime;

/// <summary>Um assembly de jogo carregado num contexto descartável, com o <see cref="Game"/> instanciado.</summary>
public sealed class LoadedGame : IDisposable
{
    private readonly GameLoadContext _context;
    private bool _disposed;

    internal LoadedGame(GameLoadContext context, Game game)
    {
        _context = context;
        Game = game;
    }

    public Game Game { get; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _context.Unload();
    }
}

internal sealed class GameLoadContext : AssemblyLoadContext
{
    public GameLoadContext() : base("lunet-game", isCollectible: true) { }

    // Dependências (Lunet.Framework, BCL) resolvem no contexto padrão.
    protected override Assembly? Load(AssemblyName assemblyName) => null;
}

public sealed class GameLoadException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Carrega o assembly compilado de um jogo e instancia sua classe <see cref="Game"/>.</summary>
public static class GameLoader
{
    public static LoadedGame Load(byte[] assembly, byte[]? symbols = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var context = new GameLoadContext();
        try
        {
            using var assemblyStream = new MemoryStream(assembly, writable: false);
            using var symbolStream = symbols is null ? null : new MemoryStream(symbols, writable: false);
            var loaded = context.LoadFromStream(assemblyStream, symbolStream);

            var gameTypes = FindGameTypes(loaded);
            if (gameTypes.Count == 0)
                throw new GameLoadException("Nenhuma classe pública que herde de Lunet.Game foi encontrada. Crie uma, por exemplo: public sealed class MeuJogo : Game.");
            if (gameTypes.Count > 1)
                throw new GameLoadException("Mais de uma classe herda de Lunet.Game: " + string.Join(", ", gameTypes.Select(t => t.FullName)) + ". Mantenha apenas uma.");

            var type = gameTypes[0];
            if (type.GetConstructor(Type.EmptyTypes) is null)
                throw new GameLoadException($"{type.FullName} precisa de um construtor público sem parâmetros.");

            var game = (Game)Activator.CreateInstance(type)!;
            return new LoadedGame(context, game);
        }
        catch (GameLoadException)
        {
            context.Unload();
            throw;
        }
        catch (Exception ex)
        {
            context.Unload();
            throw new GameLoadException("Falha ao carregar o jogo: " + ex.Message, ex);
        }
    }

    private static List<Type> FindGameTypes(Assembly assembly)
    {
        var result = new List<Type>();
        foreach (var type in assembly.GetExportedTypes())
        {
            if (!type.IsAbstract && !type.IsGenericTypeDefinition && typeof(Game).IsAssignableFrom(type))
                result.Add(type);
        }
        return result;
    }
}
