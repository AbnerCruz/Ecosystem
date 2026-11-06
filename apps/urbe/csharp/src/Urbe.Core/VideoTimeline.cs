using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Urbe.Core;

/// <summary>A scene placed on the timeline with its script read from the note at build time.</summary>
public sealed record VideoTimelineScene(
    VideoScene Scene, PublicationPlacement Placement, double Start, double Duration, double FadeIn,
    string? Title, string Script, string ContentKey)
{
    public double End => Start + Duration;
}

/// <summary>An element as it is at one instant: every animated property resolved.</summary>
public sealed record VideoElementState(
    string Id, string Kind, string Text, string? Source, double X, double Y, double Width, double Height,
    double Scale, double Opacity, double Rotation, double Progress, string Color, double FontSize);

/// <summary>
/// A scene drawn at one instant. <see cref="StateKey"/> is equal for two instants that look the same (a still
/// scene, or one whose animations ended), so a rendered layer is reused instead of drawn again.
/// </summary>
public sealed record VideoLayer(VideoTimelineScene Scene, double Opacity, double LocalTime, IReadOnlyList<VideoElementState> Elements, string StateKey);

/// <summary>What the video shows at a time: scene layers bottom to top, and the subtitle over them.</summary>
public sealed record VideoFrame(double Time, IReadOnlyList<VideoLayer> Layers, string? Subtitle);

/// <summary>
/// CST-209: the deterministic evaluation of a video publication. Build reads the scripts of the notes once; Evaluate
/// gives the same frame for the same time, with no clock or randomness, so preview and export agree frame by frame.
/// </summary>
public sealed class VideoTimeline
{
    private readonly Dictionary<string, FunctionPlot.Expression> _motions = new(StringComparer.Ordinal);

    private VideoTimeline(int width, int height, int fps, IReadOnlyList<VideoTimelineScene> scenes, IReadOnlyList<VideoCue> cues,
        IReadOnlyList<VideoAudioClip> audio, IReadOnlyList<string> missing, bool subtitles)
    {
        Width = width; Height = height; Fps = fps; Scenes = scenes; Cues = cues; Audio = audio; MissingDocumentIds = missing;
        BurnSubtitles = subtitles;
        Duration = scenes.Count == 0 ? 0 : scenes[^1].End;
    }

    public int Width { get; }
    public int Height { get; }
    public int Fps { get; }
    public double Duration { get; }
    public bool BurnSubtitles { get; }
    public int FrameCount => (int)Math.Ceiling(Duration * Fps - 1e-6);
    public IReadOnlyList<VideoTimelineScene> Scenes { get; }
    public IReadOnlyList<VideoCue> Cues { get; }
    public IReadOnlyList<VideoAudioClip> Audio { get; }
    public IReadOnlyList<string> MissingDocumentIds { get; }
    public bool IsComplete => MissingDocumentIds.Count == 0;

    public static VideoTimeline Build(PublicationProject project, PublicationSourceCatalog catalog, int? resolution = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return Build(project, reference =>
        {
            try { return catalog.Read(reference) is { } view ? (view.Path, view.Markdown) : null; }
            catch (NotSupportedException) { return null; }
        }, resolution);
    }

    /// <param name="read">The current path and Markdown of a source, or null when it is missing.</param>
    /// <param name="resolution">A different short side (a lighter preview); the look is the same, being proportional.</param>
    public static VideoTimeline Build(PublicationProject project, Func<PublicationSourceReference, (string Path, string Markdown)?> read, int? resolution = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(read);
        var cache = new Dictionary<PublicationSourceReference, (string Path, string Markdown)?>();
        (string Path, string Markdown)? Read(PublicationSourceReference r) => cache.TryGetValue(r, out var v) ? v : cache[r] = read(r);
        project = VideoComposition.Synced(project, r => Read(r)?.Markdown);
        PublicationProjectCodec.Validate(project);
        var video = project.Video!;
        if (resolution is { } res) video = video with { Resolution = Math.Clamp(res, VideoComposition.MinHeight, VideoComposition.MaxHeight) };
        var (width, height) = video.FrameSize;
        var scenes = new List<VideoTimelineScene>();
        var cues = new List<VideoCue>();
        var missing = new List<string>();
        double end = 0;
        for (int i = 0; i < project.Sections.Count; i++)
        {
            var placement = project.Sections[i];
            var scene = video.Scenes[i];
            var source = Read(placement.Source);
            if (source is null && !missing.Contains(placement.Source.DocumentId)) missing.Add(placement.Source.DocumentId);
            var plain = VideoScript.Plain(source?.Markdown);
            var title = VideoScript.Title(source?.Markdown) ?? (source is { } s ? Path.GetFileNameWithoutExtension(s.Path) : null);
            double fade = scene.Transition.Kind == "fade" ? Math.Min(scene.Transition.Seconds, scene.Duration / 2) : 0;
            // a crossfade overlaps the previous scene; the first one fades in from black
            double overlap = i == 0 ? 0 : Math.Min(fade, scenes[^1].Duration / 2);
            double start = Math.Max(0, end - overlap);
            var key = Hash(JsonSerializer.Serialize(scene) + "\u0001" + title + "\u0001" + plain + "\u0001" + width + "x" + height);
            var placed = new VideoTimelineScene(scene, placement, start, scene.Duration, fade, title, plain, key);
            scenes.Add(placed);
            // subtitles of a scene start after its entrance and end before the next one's
            cues.AddRange(VideoScript.Cues(plain, start + overlap, start + scene.Duration));
            end = placed.End;
        }
        return new VideoTimeline(width, height, video.Fps, scenes, cues, video.Audio.ToList(), missing, video.Subtitles);
    }

    public double FrameTime(int index) => index / (double)Fps;

    public VideoFrame Frame(int index) => Evaluate(FrameTime(index));

    public VideoFrame Evaluate(double t)
    {
        t = Math.Clamp(t, 0, Math.Max(0, Duration - 1e-9));
        var layers = new List<VideoLayer>();
        foreach (var scene in Scenes)
        {
            if (t < scene.Start || t >= scene.End) continue;
            var local = t - scene.Start;
            var opacity = scene.FadeIn > 0 && local < scene.FadeIn ? Ease("easeInOut", local / scene.FadeIn) : 1;
            var states = scene.Scene.Elements.Select(e => State(scene, e, local)).Where(s => s is not null).Select(s => s!).ToList();
            var sb = new StringBuilder(scene.ContentKey);
            foreach (var s in states)
                sb.Append('|').Append(s.Id).Append(';').Append(F(s.X)).Append(';').Append(F(s.Y)).Append(';').Append(F(s.Scale))
                  .Append(';').Append(F(s.Opacity)).Append(';').Append(F(s.Rotation)).Append(';').Append(F(s.Progress));
            layers.Add(new VideoLayer(scene, opacity, local, states, Hash(sb.ToString())));
        }
        string? subtitle = BurnSubtitles ? Cues.LastOrDefault(c => t >= c.Start && t < c.End)?.Text : null;
        return new VideoFrame(t, layers, subtitle);
    }

    public string Srt() => VideoScript.Srt(Cues);
    public string Vtt() => VideoScript.Vtt(Cues);

    private VideoElementState? State(VideoTimelineScene scene, VideoElement e, double local)
    {
        if (local < e.Start || e.End is { } end && local >= end) return null;
        double Value(string property, double baseValue)
        {
            var frames = e.Keyframes.Where(k => k.Property == property).OrderBy(k => k.Time).ToList();
            var v = frames.Count == 0 ? baseValue : Interpolate(frames, local);
            foreach (var motion in e.Motions)
                if (motion.Property == property) v += Motion(motion.Expression, local);
            return v;
        }
        var text = e.Kind switch
        {
            "title" => e.Text ?? scene.Title ?? "",
            "script" => scene.Script,
            _ => e.Text ?? ""
        };
        return new VideoElementState(e.Id, e.Kind, text, e.Source, Value("x", e.X), Value("y", e.Y), e.Width, e.Height,
            Math.Max(0, Value("scale", 1)), Math.Clamp(Value("opacity", 1), 0, 1), Value("rotation", 0),
            Math.Clamp(Value("progress", 1), 0, 1), e.Color, e.FontSize);
    }

    private double Motion(string expression, double t)
    {
        if (!_motions.TryGetValue(expression, out var parsed))
            _motions[expression] = parsed = FunctionPlot.Expression.Parse(expression, "t");
        var v = parsed.Evaluate(t);
        return double.IsFinite(v) ? v : 0;
    }

    /// <summary>The value at <paramref name="t"/> between keyframes (held before the first and after the last).</summary>
    public static double Interpolate(IReadOnlyList<VideoKeyframe> sorted, double t)
    {
        if (t <= sorted[0].Time) return sorted[0].Value;
        for (int i = 1; i < sorted.Count; i++)
        {
            var b = sorted[i];
            if (t > b.Time) continue;
            var a = sorted[i - 1];
            var span = b.Time - a.Time;
            var u = span <= 0 ? 1 : (t - a.Time) / span;
            return a.Value + (b.Value - a.Value) * Ease(b.Easing, u);
        }
        return sorted[^1].Value;
    }

    public static double Ease(string easing, double u)
    {
        u = Math.Clamp(u, 0, 1);
        return easing switch
        {
            "linear" => u,
            "easeIn" => u * u * u,
            "easeOut" => 1 - Math.Pow(1 - u, 3),
            "step" => u >= 1 ? 1 : 0,
            _ => u < 0.5 ? 4 * u * u * u : 1 - Math.Pow(-2 * u + 2, 3) / 2
        };
    }

    private static string F(double v) => Math.Round(v, 4).ToString("R", CultureInfo.InvariantCulture);

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)), 0, 16);
}

/// <summary>
/// Selective cache of rendered scene layers, keyed by <see cref="VideoLayer.StateKey"/> and bounded in bytes (least
/// recently used out first). Editing one scene changes only its keys: the other scenes come from the cache.
/// </summary>
public sealed class VideoFrameCache<T> : IDisposable where T : class
{
    private readonly long _budget;
    private readonly Func<T, long> _size;
    private readonly Dictionary<string, LinkedListNode<(string Key, T Value, long Size)>> _map = new(StringComparer.Ordinal);
    private readonly LinkedList<(string Key, T Value, long Size)> _order = new();
    private readonly object _gate = new();
    private long _bytes;

    public VideoFrameCache(long budgetBytes, Func<T, long> size)
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
