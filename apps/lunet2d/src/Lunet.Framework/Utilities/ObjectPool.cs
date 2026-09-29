namespace Lunet;

/// <summary>Pool de objetos reutilizáveis para evitar alocações no loop do jogo.</summary>
public sealed class ObjectPool<T> where T : class
{
    private readonly Stack<T> _free = new();
    private readonly Func<T> _create;
    private readonly Action<T>? _reset;
    private readonly int _maxRetained;

    /// <summary>Cria um pool.</summary>
    /// <param name="create">Fábrica chamada quando o pool está vazio.</param>
    /// <param name="reset">Ação que limpa um objeto devolvido.</param>
    /// <param name="maxRetained">Quantos objetos livres guardar no máximo.</param>
    public ObjectPool(Func<T> create, Action<T>? reset = null, int maxRetained = 256)
    {
        _create = create ?? throw new ArgumentNullException(nameof(create));
        _reset = reset;
        _maxRetained = maxRetained;
    }

    /// <summary>Objetos criados que ainda não voltaram ao pool (em uso).</summary>
    public int InUse { get; private set; }

    /// <summary>Quantos objetos livres o pool guarda.</summary>
    public int Available => _free.Count;

    /// <summary>Pega um objeto do pool (ou cria um novo).</summary>
    /// <returns>Um objeto pronto para uso.</returns>
    public T Get()
    {
        InUse++;
        return _free.Count > 0 ? _free.Pop() : _create();
    }

    /// <summary>Devolve um objeto ao pool depois de usá-lo.</summary>
    /// <param name="item">Objeto a devolver.</param>
    public void Return(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (InUse > 0) InUse--;
        _reset?.Invoke(item);
        if (_free.Count < _maxRetained) _free.Push(item);
    }
}
