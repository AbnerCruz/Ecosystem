namespace Lunet;

/// <summary>Registro simples de serviços do jogo, por tipo. O host registra os serviços padrão; o jogo pode adicionar os seus.</summary>
public sealed class GameServices
{
    private readonly Dictionary<Type, object> _services = new();

    public void Add<T>(T service) where T : class
    {
        ArgumentNullException.ThrowIfNull(service);
        _services[typeof(T)] = service;
    }

    public T Get<T>() where T : class =>
        TryGet<T>(out var service) ? service : throw new InvalidOperationException($"Serviço {typeof(T).Name} não registrado.");

    public bool TryGet<T>(out T service) where T : class
    {
        if (_services.TryGetValue(typeof(T), out var found)) { service = (T)found; return true; }
        service = null!;
        return false;
    }

    public bool Remove<T>() where T : class => _services.Remove(typeof(T));
}
