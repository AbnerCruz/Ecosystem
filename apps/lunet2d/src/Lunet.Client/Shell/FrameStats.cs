using System.Globalization;
using System.Text;

namespace Lunet.Client.Shell;

/// <summary>One measured frame of the game running in the shell, in CPU milliseconds.</summary>
/// <param name="Total">Update + draw + rasterization of the frame.</param>
/// <param name="Update">The fixed update steps of the frame (zero when none ran).</param>
/// <param name="Draw">The game's Draw call.</param>
/// <param name="Steps">How many fixed update steps ran in the frame.</param>
public readonly record struct FrameSample(double Total, double Update, double Draw, int Steps);

/// <summary>
/// The last frames of the game, for the Desempenho panel (M6). As in Godot's monitors, the history is kept while the
/// game runs even with the panel closed, so opening it later shows what already happened; a single frame over the
/// budget is a hitch, so the panel shows each frame, not only the average (Unity's profiling guidance).
/// </summary>
public sealed class FrameStats
{
    /// <summary>Frames kept: five seconds at 60 frames per second.</summary>
    public const int Capacity = 300;

    /// <summary>The 60 Hz frame budget.</summary>
    public const double Budget60 = 1000 / 60.0;

    /// <summary>The 30 Hz frame budget.</summary>
    public const double Budget30 = 1000 / 30.0;

    private readonly FrameSample[] _samples = new FrameSample[Capacity];
    private int _next;

    /// <summary>Frames stored (at most <see cref="Capacity"/>).</summary>
    public int Count { get; private set; }

    /// <summary>Frames recorded since the game started, including those no longer kept.</summary>
    public long Recorded { get; private set; }

    public void Add(FrameSample sample)
    {
        _samples[_next] = sample;
        _next = (_next + 1) % Capacity;
        if (Count < Capacity) Count++;
        Recorded++;
    }

    public void Clear()
    {
        _next = 0;
        Count = 0;
        Recorded = 0;
    }

    /// <summary>The kept frames, oldest first.</summary>
    public IReadOnlyList<FrameSample> Samples
    {
        get
        {
            var result = new FrameSample[Count];
            var start = (_next - Count + Capacity) % Capacity;
            for (var i = 0; i < Count; i++) result[i] = _samples[(start + i) % Capacity];
            return result;
        }
    }

    public double Average => Count == 0 ? 0 : Samples.Average(s => s.Total);
    public double Max => Count == 0 ? 0 : Samples.Max(s => s.Total);

    /// <summary>The time 95% of the frames stay under (nearest rank).</summary>
    public double Percentile95
    {
        get
        {
            if (Count == 0) return 0;
            var sorted = Samples.Select(s => s.Total).OrderBy(t => t).ToArray();
            return sorted[Math.Clamp((int)Math.Ceiling(0.95 * sorted.Length) - 1, 0, sorted.Length - 1)];
        }
    }

    /// <summary>Kept frames over the 60 Hz budget: the hitches a person may feel.</summary>
    public int OverBudget => Samples.Count(s => s.Total > Budget60);

    /// <summary>Plain text of the window, to copy into a message or a note.</summary>
    public string Report(string project)
    {
        var c = CultureInfo.InvariantCulture;
        var samples = Samples;
        var text = new StringBuilder();
        text.AppendLine($"Desempenho do Lunet — {project}");
        text.AppendLine($"Últimos {Count} quadros (CPU, sem esperar a GPU)");
        text.AppendLine(string.Format(c, "Média {0:0.00} ms · 95% abaixo de {1:0.00} ms · pior {2:0.00} ms", Average, Percentile95, Max));
        text.AppendLine(string.Format(c, "Acima de 16,7 ms: {0} · acima de 33,3 ms: {1}", OverBudget, samples.Count(s => s.Total > Budget30)));
        if (Count > 0)
            text.AppendLine(string.Format(c, "Update médio {0:0.00} ms · Draw médio {1:0.00} ms · passos por quadro {2:0.00}",
                samples.Average(s => s.Update), samples.Average(s => s.Draw), samples.Average(s => s.Steps)));
        return text.ToString().TrimEnd();
    }
}
