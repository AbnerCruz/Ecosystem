namespace Lunet;

/// <summary>Pool de objetos reutilizáveis para evitar alocações no loop do jogo.</summary>
public sealed class ObjectPool<T> where T : class
{
    private readonly Stack<T> _free = new();
    private readonly Func<T> _create;
    private readonly Action<T>? _reset;
    private readonly int _maxRetained;

    public ObjectPool(Func<T> create, Action<T>? reset = null, int maxRetained = 256)
    {
        _create = create ?? throw new ArgumentNullException(nameof(create));
        _reset = reset;
        _maxRetained = maxRetained;
    }

    /// <summary>Objetos criados que ainda não voltaram ao pool (em uso).</summary>
    public int InUse { get; private set; }

    public int Available => _free.Count;

    public T Get()
    {
        InUse++;
        return _free.Count > 0 ? _free.Pop() : _create();
    }

    public void Return(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (InUse > 0) InUse--;
        _reset?.Invoke(item);
        if (_free.Count < _maxRetained) _free.Push(item);
    }
}
