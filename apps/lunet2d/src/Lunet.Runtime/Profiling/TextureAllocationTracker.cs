using System.Threading;

namespace Lunet.Runtime.Profiling;

/// <summary>Conta bytes RGBA8 conhecidos do renderizador: não é VRAM total, não inclui driver, shaders e buffers.</summary>
public sealed class TextureAllocationTracker
{
    private readonly Dictionary<int, long> _live = new();
    private long _liveBytes, _peakBytes;
    private int _count;

    /// <summary>Bytes RGBA8 dos recursos vivos registrados.</summary>
    public long LiveBytes => Interlocked.Read(ref _liveBytes);
    /// <summary>Pico conhecido desde o último Reset.</summary>
    public long PeakBytes => Interlocked.Read(ref _peakBytes);
    /// <summary>Número de texturas registradas e ainda vivas.</summary>
    public int LiveCount => Volatile.Read(ref _count);

    /// <summary>Registra textura após criação GL bem-sucedida, inclusive o backing de render target, apenas uma vez.</summary>
    /// <param name="handle">Handle GL positivo e não registrado.</param>
    /// <param name="width">Largura em pixels.</param>
    /// <param name="height">Altura em pixels.</param>
    public void Track(int handle, int width, int height)
    {
        if (handle <= 0) throw new ArgumentOutOfRangeException(nameof(handle));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        long bytes = checked((long)width * height * 4L);
        if (!_live.TryAdd(handle, bytes)) throw new InvalidOperationException("Handle GL já registrado.");
        long total = Interlocked.Add(ref _liveBytes, bytes);
        if (total > _peakBytes) Interlocked.Exchange(ref _peakBytes, total);
        Volatile.Write(ref _count, _live.Count);
    }

    /// <summary>Esquece recurso destruído; dupla liberação não subtrai bytes duas vezes.</summary>
    /// <param name="handle">Handle GL previamente registrado.</param>
    /// <returns>True apenas quando removido.</returns>
    public bool Untrack(int handle)
    {
        if (!_live.Remove(handle, out long bytes)) return false;
        Interlocked.Add(ref _liveBytes, -bytes);
        Volatile.Write(ref _count, _live.Count);
        return true;
    }

    /// <summary>Zera o histórico após perda/destruição do contexto GL.</summary>
    public void Reset()
    {
        _live.Clear();
        Interlocked.Exchange(ref _liveBytes, 0);
        Interlocked.Exchange(ref _peakBytes, 0);
        Volatile.Write(ref _count, 0);
    }
}
