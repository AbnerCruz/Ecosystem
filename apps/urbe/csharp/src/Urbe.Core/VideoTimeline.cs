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

    /// <summary>
    /// #1004 slice 5: what the scene draws — its elements with every component placed expanded. <see cref="Scene"/>
    /// stays as saved (a "use" stays one element), so an edit never writes the expansion back.
    /// </summary>
    public IReadOnlyList<VideoElement> Elements { get; init; } = Scene.Elements;

    /// <summary>When the scene looks formed: its entrance and every keyframe done, before its end (a poster frame).</summary>
    public double Settled
    {
        get
        {
            double t = FadeIn;
            foreach (var e in Scene.Elements)
            {
                t = Math.Max(t, e.Start);
                foreach (var k in e.Keyframes) t = Math.Max(t, k.Time);
            }
            return Start + Math.Min(t, Duration * 0.95);
        }
    }
}

/// <summary>An element as it is at one instant: every animated property resolved, in frame coordinates.</summary>
/// <param name="Parent">For an element that follows another, where its parent is (to turn a drag back into an offset).</param>
public sealed record VideoElementState(
    string Id, string Kind, string Text, string? Source, double X, double Y, double Width, double Height,
    double Scale, double Opacity, double Rotation, double Progress, string Color, double FontSize, VideoParentFrame? Parent = null,
    int Instance = -1, string? Split = null, VideoEffects? Effects = null, string? Clip = null);

/// <summary>#1004 slice 3: an element's effects at one instant (radii as fractions of the frame's height).</summary>
public sealed record VideoEffects(double Glow, string? GlowColor, double Shadow, double Blur, string? Gradient)
{
    public bool Any => Glow > 0 || Shadow > 0 || Blur > 0 || Gradient is not null;
}

/// <summary>A parent's place at one instant and the frame's size: what turns a child's offset into frame coordinates.</summary>
public sealed record VideoParentFrame(double X, double Y, double Scale, double Rotation, double Opacity, int Width, int Height)
{
    public (double X, double Y) ToFrame(double ox, double oy)
    {
        double a = Rotation * Math.PI / 180, dx = ox * Width * Scale, dy = oy * Height * Scale;
        return (X + (dx * Math.Cos(a) - dy * Math.Sin(a)) / Width, Y + (dx * Math.Sin(a) + dy * Math.Cos(a)) / Height);
    }

    public (double X, double Y) ToOffset(double fx, double fy)
    {
        double a = -Rotation * Math.PI / 180, dx = (fx - X) * Width, dy = (fy - Y) * Height, s = Math.Max(1e-6, Scale);
        return ((dx * Math.Cos(a) - dy * Math.Sin(a)) / s / Width, (dx * Math.Sin(a) + dy * Math.Cos(a)) / s / Height);
    }
}

/// <summary>
/// A scene drawn at one instant. <see cref="StateKey"/> is equal for two instants that look the same (a still
/// scene, or one whose animations ended), so a rendered layer is reused instead of drawn again.
/// </summary>
public sealed record VideoLayer(VideoTimelineScene Scene, double Opacity, double LocalTime, IReadOnlyList<VideoElementState> Elements, string StateKey,
    VideoCameraState? Camera = null)
{
    /// <summary>#1004: how the scene is entering — its transition kind and how far along (0 to 1, eased); 1 once in.</summary>
    public string Entrance => Scene.Scene.Transition.Kind;
    public double EntranceProgress => Scene.FadeIn > 0 && LocalTime < Scene.FadeIn ? Opacity : 1;
}

/// <summary>#1004 slice 4: the camera at one instant: the frame point at the centre, the zoom and the rotation.</summary>
public sealed record VideoCameraState(double X, double Y, double Zoom, double Rotation)
{
    /// <summary>The element as the camera shows it on a <paramref name="width"/>×<paramref name="height"/> frame.</summary>
    public VideoElementState Apply(VideoElementState e, int width, int height)
    {
        double dx = (e.X - X) * width * Zoom, dy = (e.Y - Y) * height * Zoom, a = -Rotation * Math.PI / 180;
        double rx = dx * Math.Cos(a) - dy * Math.Sin(a), ry = dx * Math.Sin(a) + dy * Math.Cos(a);
        return e with { X = 0.5 + rx / width, Y = 0.5 + ry / height, Scale = e.Scale * Zoom, Rotation = e.Rotation - Rotation };
    }

    /// <summary>The scene point (fractions) the camera shows at screen point (<paramref name="sx"/>, <paramref name="sy"/>).</summary>
    public (double X, double Y) Unapply(double sx, double sy, int width, int height)
    {
        double rx = (sx - 0.5) * width, ry = (sy - 0.5) * height, a = Rotation * Math.PI / 180;
        double dx = rx * Math.Cos(a) - ry * Math.Sin(a), dy = rx * Math.Sin(a) + ry * Math.Cos(a);
        return (X + dx / (width * Zoom), Y + dy / (height * Zoom));
    }

    public bool IsStill => Math.Abs(X - 0.5) < 1e-9 && Math.Abs(Y - 0.5) < 1e-9 && Math.Abs(Zoom - 1) < 1e-9 && Math.Abs(Rotation) < 1e-9;
}

/// <summary>What the video shows at a time: scene layers bottom to top, and the subtitle over them.</summary>
public sealed record VideoFrame(double Time, IReadOnlyList<VideoLayer> Layers, string? Subtitle);

/// <summary>
/// CST-209: the deterministic evaluation of a video publication. Build reads the scripts of the notes once; Evaluate
/// gives the same frame for the same time, with no clock or randomness, so preview and export agree frame by frame.
/// </summary>
public sealed class VideoTimeline
{
    private VideoTimeline(int width, int height, int fps, IReadOnlyList<VideoTimelineScene> scenes, IReadOnlyList<VideoCue> cues,
        IReadOnlyList<VideoAudioClip> audio, IReadOnlyList<string> missing, bool subtitles, VideoTheme? theme)
    {
        Width = width; Height = height; Fps = fps; Scenes = scenes; Cues = cues; Audio = audio; MissingDocumentIds = missing;
        BurnSubtitles = subtitles; Theme = theme;
        Duration = scenes.Count == 0 ? 0 : scenes[^1].End;
    }

    public int Width { get; }
    public int Height { get; }
    public int Fps { get; }
    public double Duration { get; }
    public bool BurnSubtitles { get; }
    /// <summary>The video's theme (colours and background of what keeps the defaults), or null.</summary>
    public VideoTheme? Theme { get; }
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
            double fade = scene.Transition.Kind != "cut" ? Math.Min(scene.Transition.Seconds, scene.Duration / 2) : 0;
            // a crossfade overlaps the previous scene; the first one fades in from black
            double overlap = i == 0 ? 0 : Math.Min(fade, scenes[^1].Duration / 2);
            double start = Math.Max(0, end - overlap);
            var key = Hash(JsonSerializer.Serialize(scene) + "\u0001" + (video.Components is { Count: > 0 } && scene.Elements.Any(e => e.Kind == "use") ? JsonSerializer.Serialize(video.Components) : "") + "\u0001" + title + "\u0001" + plain + "\u0001" + width + "x" + height + "\u0001" + video.Theme);
            var placed = new VideoTimelineScene(scene, placement, start, scene.Duration, fade, title, plain, key)
            {
                Elements = VideoComponents.ExpandScene(scene, video.Components).Elements
            };
            scenes.Add(placed);
            // subtitles of a scene start after its entrance and end before the next one's — or with its narration, when known
            var voice = video.Audio.FirstOrDefault(a => a.Kind == "voice" && a.Length is not null && Math.Abs(a.Start - start) < 0.05);
            var speechEnd = voice is null ? start + scene.Duration : Math.Clamp(voice.Start + voice.Length!.Value, start + overlap + 0.5, start + scene.Duration);
            cues.AddRange(VideoScript.Cues(plain, start + overlap, speechEnd));
            end = placed.End;
        }
        return new VideoTimeline(width, height, video.Fps, scenes, cues, video.Audio.ToList(), missing, video.Subtitles, VideoThemes.Find(video.Theme));
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
            var states = States(scene, local);
            var sb = new StringBuilder(scene.ContentKey);
            foreach (var s in states)
            {
                sb.Append('|').Append(s.Id).Append('#').Append(s.Instance).Append(';').Append(F(s.X)).Append(';').Append(F(s.Y)).Append(';').Append(F(s.Scale))
                  .Append(';').Append(F(s.Opacity)).Append(';').Append(F(s.Rotation)).Append(';').Append(F(s.Progress))
                  .Append(';').Append(F(s.Width)).Append(';').Append(F(s.Height));
                if (s.Kind == "code") sb.Append(";t").Append(F(local));   // a program draws something new every instant
                if (s.Effects is { } fx) sb.Append(";fx").Append(F(fx.Glow)).Append(';').Append(F(fx.Shadow)).Append(';').Append(F(fx.Blur));
            }
            var camera = Camera(scene.Scene.Camera, local);
            if (camera is not null) sb.Append("|cam").Append(F(camera.X)).Append(';').Append(F(camera.Y)).Append(';').Append(F(camera.Zoom)).Append(';').Append(F(camera.Rotation));
            layers.Add(new VideoLayer(scene, opacity, local, states, Hash(sb.ToString()), camera));
        }
        string? subtitle = BurnSubtitles ? Cues.LastOrDefault(c => t >= c.Start && t < c.End)?.Text : null;
        return new VideoFrame(t, layers, subtitle);
    }

    /// <summary>What each scene shows (scene ID → content key), kept with the MP4 it went into.</summary>
    public Dictionary<string, string> SceneKeys() => Scenes.ToDictionary(s => s.Scene.Id, s => s.ContentKey, StringComparer.Ordinal);

    /// <summary>The scenes that look different from <paramref name="rendered"/> (or are new); null when nothing was rendered.</summary>
    public IReadOnlyList<VideoTimelineScene>? ChangedSince(IReadOnlyDictionary<string, string>? rendered) =>
        rendered is null ? null : Scenes.Where(s => !rendered.TryGetValue(s.Scene.Id, out var key) || key != s.ContentKey).ToList();

    public string Srt() => VideoScript.Srt(Cues);
    public string Vtt() => VideoScript.Vtt(Cues);

    /// <summary>The visible elements of a scene, each placed through its parents (validation rules out cycles).</summary>
    private List<VideoElementState> States(VideoTimelineScene scene, double local)
    {
        // a parent is never repeated (validation): it has at most one state
        var placed = new Dictionary<string, VideoElementState?>(StringComparer.Ordinal);
        VideoElementState? ParentState(string id)
        {
            if (placed.TryGetValue(id, out var done)) return done;
            var e = scene.Elements.First(p => p.Id == id);
            var own = State(scene, e, local, 0, 1);
            return placed[id] = own is null ? null : Place(own, e);
        }
        VideoElementState? Place(VideoElementState state, VideoElement e)
        {
            if (e.Parent is not { } parentId) return state;
            var parent = ParentState(parentId);
            if (parent is null) return null;
            var frame = new VideoParentFrame(parent.X, parent.Y, parent.Scale, parent.Rotation, parent.Opacity, Width, Height);
            var (x, y) = frame.ToFrame(state.X, state.Y);
            return state with
            {
                X = x, Y = y, Scale = state.Scale * parent.Scale, Rotation = state.Rotation + parent.Rotation,
                Opacity = state.Opacity * parent.Opacity, Parent = frame
            };
        }
        var states = new List<VideoElementState>();
        foreach (var e in scene.Elements)
        {
            int n = Math.Clamp(e.Repeat, 1, VideoProcedural.MaxRepeat);
            if (e.Split is not null)
            {
                // one instance per letter or word of the text as it is shown (the note's title for a title)
                if (State(scene, e, local, 0, 1) is not { } whole) continue;
                n = Math.Min(VideoProcedural.MaxRepeat, VideoProcedural.Units(whole.Text, e.Split).Count);
                for (int i = 0; i < n; i++)
                    if (State(scene, e, local, i, n) is { } unit && Place(unit with { Instance = i, Split = e.Split }, e) is { } at) states.Add(at);
                continue;
            }
            if (n == 1)
            {
                if (placed.TryGetValue(e.Id, out var done)) { if (done is not null) states.Add(done); continue; }
                if (State(scene, e, local, 0, 1) is { } one && Place(one, e) is { } at) states.Add(placed[e.Id] = at);
                else placed[e.Id] = null;
                continue;
            }
            for (int i = 0; i < n; i++)
                if (State(scene, e, local, i, n) is { } instance && Place(instance with { Instance = i }, e) is { } at) states.Add(at);
        }
        return states;
    }

    private VideoElementState? State(VideoTimelineScene scene, VideoElement e, double local, int index, int count)
    {
        if (local < e.Start || e.End is { } end && local >= end) return null;
        double Value(string property, double baseValue)
        {
            List<VideoKeyframe>? frames = null;
            foreach (var k in e.Keyframes)
                if (k.Property == property) (frames ??= []).Add(k);
            var v = frames is null ? baseValue : Interpolate(frames.Count > 1 ? frames.OrderBy(k => k.Time).ToList() : frames, local);
            foreach (var motion in e.Motions)
                if (motion.Property == property)
                {
                    var m = VideoProcedural.Evaluate(motion.Expression, local, index, count);
                    v = motion.Mode == "set" ? m : v + m;
                }
            return v;
        }
        var text = e.Kind switch
        {
            "title" => e.Text ?? scene.Title ?? "",
            "script" => scene.Script,
            // a function, TeX or path data written in source is read there too
            "plot" or "math" or "path" => e.Text ?? e.Source ?? "",
            _ => e.Text ?? ""
        };
        return new VideoElementState(e.Id, e.Kind, text, e.Source, Value("x", e.X), Value("y", e.Y), Math.Max(0.001, Value("width", e.Width)), Math.Max(0.001, Value("height", e.Height)),
            Math.Max(0, Value("scale", 1)), Math.Clamp(Value("opacity", 1), 0, 1), Value("rotation", 0),
            Math.Clamp(Value("progress", 1), 0, 1),
            Theme is { } theme && string.Equals(e.Color, VideoThemes.DefaultColor, StringComparison.OrdinalIgnoreCase) ? theme.ColorFor(e.Kind) : e.Color, e.FontSize)
        {
            Clip = e.Clip,
            Effects = new VideoEffects(Math.Clamp(Value("glow", e.Glow), 0, 0.2), e.GlowColor, Math.Clamp(Value("shadow", e.Shadow), 0, 0.2),
                Math.Clamp(Value("blur", e.Blur), 0, 0.2), e.Gradient) is { Any: true } fx ? fx : null
        };
    }

    /// <summary>The camera at <paramref name="local"/> seconds into its scene; null when it stays on the whole frame.</summary>
    public static VideoCameraState? Camera(VideoCamera? camera, double local)
    {
        if (camera is null || camera.Keyframes.Count == 0 && camera.Motions.Count == 0) return null;
        double Value(string property, double baseValue)
        {
            var frames = camera.Keyframes.Where(k => k.Property == property).OrderBy(k => k.Time).ToList();
            var v = frames.Count == 0 ? baseValue : Interpolate(frames, local);
            foreach (var m in camera.Motions)
                if (m.Property == property)
                {
                    var x = VideoProcedural.Evaluate(m.Expression, local);
                    v = m.Mode == "set" ? x : v + x;
                }
            return v;
        }
        var state = new VideoCameraState(Value("x", 0.5), Value("y", 0.5), Math.Clamp(Value("zoom", 1), 0.05, 50), Value("rotation", 0));
        return state.IsStill ? null : state;
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

    private static readonly CubicBezier Back = new(0.34, 1.56, 0.64, 1);

    public static double Ease(string easing, double u)
    {
        u = Math.Clamp(u, 0, 1);
        return easing switch
        {
            "linear" => u,
            "easeIn" => u * u * u,
            "easeOut" => 1 - Math.Pow(1 - u, 3),
            "step" => u >= 1 ? 1 : 0,
            "back" => Back.Solve(u),
            "easeInOut" => u < 0.5 ? 4 * u * u * u : 1 - Math.Pow(-2 * u + 2, 3) / 2,
            _ when CubicBezier.TryParse(easing, out var curve) => curve.Solve(u),
            _ => u < 0.5 ? 4 * u * u * u : 1 - Math.Pow(-2 * u + 2, 3) / 2
        };
    }

    private static string F(double v) => Math.Round(v, 4).ToString("R", CultureInfo.InvariantCulture);

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)), 0, 16);
}
