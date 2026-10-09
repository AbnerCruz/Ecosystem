namespace Lunet.Runtime.Profiling;

/// <summary>Janela de medições CPU do Preview. Não mede tempo de GPU nem separa Update/Draw.</summary>
public readonly record struct PreviewFrameSnapshot(
    int Samples, double FramesPerSecond, double FrameMilliseconds, double CpuTickMilliseconds,
    double DrawCallsPerFrame, double TrianglesPerFrame, double AllocatedBytesPerFrame, int SlowFrames);

/// <summary>
/// Janela circular de métricas reais do Preview; escreve na thread de GL e fornece snapshots consistentes
/// para UI, sem objetos ou arrays novos no caminho quente. Não altera o jogo nem seu estado persistido.
/// </summary>
public sealed class PreviewFrameStatistics
{
    private readonly record struct Sample(double Elapsed, double Cpu, int DrawCalls, int Triangles, long Allocations);

    private readonly object _sync = new();
    private readonly Sample[] _window;
    private int _count, _next;

    public PreviewFrameStatistics(int capacity = 90)
    {
        if (capacity is < 2 or > 600) throw new ArgumentOutOfRangeException(nameof(capacity));
        _window = new Sample[capacity];
    }

    /// <summary>Registra um quadro real. Delta é tempo entre OnDrawFrame, CPU é a duração de GameHost.Tick.</summary>
    public void Record(double elapsedSeconds, double cpuMilliseconds, int drawCalls, int triangles, long allocationBytes)
    {
        if (!(elapsedSeconds > 0) || !double.IsFinite(elapsedSeconds))
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (!double.IsFinite(cpuMilliseconds) || cpuMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(cpuMilliseconds));
        if (drawCalls < 0) throw new ArgumentOutOfRangeException(nameof(drawCalls));
        if (triangles < 0) throw new ArgumentOutOfRangeException(nameof(triangles));
        if (allocationBytes < 0) throw new ArgumentOutOfRangeException(nameof(allocationBytes));

        lock (_sync)
        {
            _window[_next] = new Sample(elapsedSeconds, cpuMilliseconds, drawCalls, triangles, allocationBytes);
            _next = (_next + 1) % _window.Length;
            if (_count < _window.Length) _count++;
        }
    }

    /// <summary>Snapshot de médias na janela recente, inclusive quadros pausados que continuam desenhando.</summary>
    public PreviewFrameSnapshot Snapshot()
    {
        lock (_sync)
        {
            if (_count == 0) return default;
            double seconds = 0, cpu = 0, calls = 0, triangles = 0, alloc = 0;
            int slow = 0;
            for (int i = 0; i < _count; i++)
            {
                var frame = _window[i];
                seconds += frame.Elapsed;
                cpu += frame.Cpu;
                calls += frame.DrawCalls;
                triangles += frame.Triangles;
                alloc += frame.Allocations;
                if (frame.Elapsed > 1.0 / 30) slow++;
            }
            return new PreviewFrameSnapshot(
                _count, _count / seconds, seconds * 1000 / _count, cpu / _count,
                calls / _count, triangles / _count, alloc / _count, slow);
        }
    }

    /// <summary>Zera todos os dados ao desativar o painel ou reiniciar o Preview.</summary>
    public void Reset()
    {
        lock (_sync)
        {
            Array.Clear(_window);
            _count = _next = 0;
        }
    }
}
