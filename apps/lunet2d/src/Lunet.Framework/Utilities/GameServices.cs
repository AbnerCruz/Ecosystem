namespace Lunet;

/// <summary>Registro simples de serviços do jogo, por tipo. O host registra os serviços padrão; o jogo pode adicionar os seus.</summary>
public sealed class GameServices
{
    private readonly Dictionary<Type, object> _services = new();

    /// <summary>Registra um serviço, substituindo outro do mesmo tipo.</summary>
    /// <param name="service">Instância do serviço.</param>
    public void Add<T>(T service) where T : class
    {
        ArgumentNullException.ThrowIfNull(service);
        _services[typeof(T)] = service;
    }

    /// <summary>Obtém o serviço do tipo pedido.</summary>
    /// <returns>O serviço registrado. Lança InvalidOperationException se não houver.</returns>
    public T Get<T>() where T : class =>
        TryGet<T>(out var service) ? service : throw new InvalidOperationException($"Serviço {typeof(T).Name} não registrado.");

    /// <summary>Tenta obter o serviço do tipo pedido.</summary>
    /// <param name="service">Recebe o serviço, se existir.</param>
    /// <returns>Verdadeiro se o serviço está registrado.</returns>
    public bool TryGet<T>(out T service) where T : class
    {
        if (_services.TryGetValue(typeof(T), out var found)) { service = (T)found; return true; }
        service = null!;
        return false;
    }

    /// <summary>Remove o serviço do tipo pedido.</summary>
    /// <returns>Verdadeiro se havia um serviço para remover.</returns>
    public bool Remove<T>() where T : class => _services.Remove(typeof(T));
}
