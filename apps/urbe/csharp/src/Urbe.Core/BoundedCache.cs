namespace Urbe.Core;

/// <summary>
/// A derived-result cache bounded in bytes, least recently used out first. It only keeps what a pure function made from
/// its key (never canonical content): the video's scene layers by <see cref="VideoLayer.StateKey"/>, where editing one
/// scene changes only its keys, and the formulas drawn by <see cref="MathRenderer"/>.
/// </summary>
public sealed class BoundedCache<T> : IDisposable where T : class
{
    private readonly long _budget;
    private readonly Func<T, long> _size;
    private readonly Dictionary<string, LinkedListNode<(string Key, T Value, long Size)>> _map = new(StringComparer.Ordinal);
    private readonly LinkedList<(string Key, T Value, long Size)> _order = new();
    private readonly object _gate = new();
    private long _bytes;

    public BoundedCache(long budgetBytes, Func<T, long> size)
    {
        _budget = Math.Max(0, budgetBytes);
        _size = size ?? throw new ArgumentNullException(nameof(size));
    }

    public int Hits { get; private set; }
    public int Misses { get; private set; }
    public int Count { get { lock (_gate) return _map.Count; } }
    public long Bytes { get { lock (_gate) return _bytes; } }

    /// <summary>The cached value for the key, or the one <paramref name="render"/> makes (kept while it fits).</summary>
    public T GetOrAdd(string key, Func<T> render)
    {
        lock (_gate)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                Hits++;
                return node.Value.Value;
            }
            Misses++;
        }
        var value = render();
        var size = _size(value);
        lock (_gate)
        {
            if (size > _budget || _map.ContainsKey(key)) return value;
            _map[key] = _order.AddFirst((key, value, size));
            _bytes += size;
            while (_bytes > _budget && _order.Last is { } last && !ReferenceEquals(last.Value.Value, value))
            {
                _order.RemoveLast();
                _map.Remove(last.Value.Key);
                _bytes -= last.Value.Size;
                (last.Value.Value as IDisposable)?.Dispose();
            }
        }
        return value;
    }

    /// <summary>True when the cache owns <paramref name="value"/> (the caller must not dispose it).</summary>
    public bool Owns(T value)
    {
        lock (_gate) return _map.Values.Any(n => ReferenceEquals(n.Value.Value, value));
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var item in _order) (item.Value as IDisposable)?.Dispose();
            _order.Clear();
            _map.Clear();
            _bytes = 0;
        }
    }
}
