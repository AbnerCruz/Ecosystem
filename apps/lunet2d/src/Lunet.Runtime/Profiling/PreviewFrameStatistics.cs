namespace Lunet.Runtime.Profiling;

/// <summary>Médias reais de quadros do Preview, separando CPU Update e Draw quando instrumentados pelo GameHost.</summary>
public readonly record struct PreviewFrameSnapshot(
    int Samples, double FramesPerSecond, double FrameMilliseconds, double CpuTickMilliseconds,
    double DrawCallsPerFrame, double TrianglesPerFrame, double AllocatedBytesPerFrame, int SlowFrames,
    int PhasedSamples = 0, double UpdateCpuMilliseconds = 0, double DrawCpuMilliseconds = 0,
    double UpdateStepsPerFrame = 0)
{
    /// <summary>Mediana da duração entre quadros efetivamente apresentados ao Preview (P50), em ms.</summary>
    public double P50FrameMilliseconds { get; init; }
    /// <summary>Percentil 95 da duração entre quadros (nearest-rank), em ms.</summary>
    public double P95FrameMilliseconds { get; init; }
    /// <summary>Maior intervalo entre quadros da janela, em ms.</summary>
    public double WorstFrameMilliseconds { get; init; }
    /// <summary>Quadros cujo intervalo supera tanto 1,75x a mediana quanto a mediana + 2 ms.</summary>
    public int HitchFrames { get; init; }
}

/// <summary>
/// Janela circular de métricas reais do Preview; escreve na thread de GL e fornece snapshots consistentes
/// para UI, sem objetos ou arrays novos no caminho quente. Não altera o jogo nem seu estado persistido.
/// </summary>
public sealed class PreviewFrameStatistics
{
    private readonly record struct Sample(double Elapsed, double Cpu, int DrawCalls, int Triangles, long Allocations,
        bool HasPhases, double Update, double Draw, int Steps);

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
        => Record(elapsedSeconds, cpuMilliseconds, drawCalls, triangles, allocationBytes, 0, 0, 0, false);

    /// <summary>Registra o quadro incluindo fases CPU reais do GameHost (Update acumulado e Draw), sem GPU.</summary>
    public void Record(double elapsedSeconds, double cpuMilliseconds, int drawCalls, int triangles, long allocationBytes,
        double updateCpuMilliseconds, double drawCpuMilliseconds, int updateSteps)
        => Record(elapsedSeconds, cpuMilliseconds, drawCalls, triangles, allocationBytes,
            updateCpuMilliseconds, drawCpuMilliseconds, updateSteps, true);

    private void Record(double elapsedSeconds, double cpuMilliseconds, int drawCalls, int triangles, long allocationBytes,
        double updateCpuMilliseconds, double drawCpuMilliseconds, int updateSteps, bool hasPhases)
    {
        if (!(elapsedSeconds > 0) || !double.IsFinite(elapsedSeconds))
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (!double.IsFinite(cpuMilliseconds) || cpuMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(cpuMilliseconds));
        if (drawCalls < 0) throw new ArgumentOutOfRangeException(nameof(drawCalls));
        if (triangles < 0) throw new ArgumentOutOfRangeException(nameof(triangles));
        if (allocationBytes < 0) throw new ArgumentOutOfRangeException(nameof(allocationBytes));
        if (!double.IsFinite(updateCpuMilliseconds) || updateCpuMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(updateCpuMilliseconds));
        if (!double.IsFinite(drawCpuMilliseconds) || drawCpuMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(drawCpuMilliseconds));
        if (updateSteps < 0) throw new ArgumentOutOfRangeException(nameof(updateSteps));

        lock (_sync)
        {
            _window[_next] = new Sample(elapsedSeconds, cpuMilliseconds, drawCalls, triangles, allocationBytes,
                hasPhases, updateCpuMilliseconds, drawCpuMilliseconds, updateSteps);
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
            double updates = 0, draws = 0, steps = 0;
            int slow = 0, phased = 0;
            // Até 600 intervalos; o buffer na pilha evita GC mesmo com o painel aberto.
            Span<double> intervals = stackalloc double[_count];
            for (int i = 0; i < _count; i++)
            {
                var frame = _window[i];
                intervals[i] = frame.Elapsed;
                seconds += frame.Elapsed;
                cpu += frame.Cpu;
                calls += frame.DrawCalls;
                triangles += frame.Triangles;
                alloc += frame.Allocations;
                if (frame.HasPhases)
                {
                    phased++;
                    updates += frame.Update;
                    draws += frame.Draw;
                    steps += frame.Steps;
                }
                if (frame.Elapsed > 1.0 / 30) slow++;
            }
            intervals.Sort();
            double median = (_count & 1) != 0
                ? intervals[_count / 2]
                : (intervals[_count / 2 - 1] + intervals[_count / 2]) * 0.5;
            int p95Index = (int)Math.Ceiling(_count * 0.95) - 1;
            const double HitchFactor = 1.75; // Adapta-se a 60/90/120 Hz.
            const double HitchSlackSeconds = 0.002; // Ignora jitter ínfimo.
            double hitchThreshold = Math.Max(median * HitchFactor, median + HitchSlackSeconds);
            int hitches = 0;
            for (int i = 0; i < _count; i++)
                if (intervals[i] > hitchThreshold) hitches++;

            return new PreviewFrameSnapshot(
                _count, _count / seconds, seconds * 1000 / _count, cpu / _count,
                calls / _count, triangles / _count, alloc / _count, slow,
                phased, phased == 0 ? 0 : updates / phased, phased == 0 ? 0 : draws / phased,
                phased == 0 ? 0 : steps / phased)
            {
                P50FrameMilliseconds = median * 1000,
                P95FrameMilliseconds = intervals[p95Index] * 1000,
                WorstFrameMilliseconds = intervals[_count - 1] * 1000,
                HitchFrames = hitches
            };
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
