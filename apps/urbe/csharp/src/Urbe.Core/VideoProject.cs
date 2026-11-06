using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Urbe.Core;

/// <summary>
/// CST-170: the editable audiovisual document of a Publication whose layout is "video". It only holds presentation
/// and time — scenes, objects, keyframes, motions, transitions and references to audio files of the vault. The script
/// (what is said and subtitled) stays in the notes the scenes reference: editing it edits the note; editing timing
/// edits only this composition. Every scene, element and clip has a stable ID, kept across reopen and reorder.
/// </summary>
public sealed record VideoComposition
{
    public static readonly string[] Aspects = ["16:9", "9:16", "1:1"];
    public const int MinHeight = 144, MaxHeight = 2160, MaxFps = 60;

    /// <summary>16:9 (landscape), 9:16 (vertical) or 1:1.</summary>
    public string Aspect { get; init; } = "16:9";
    /// <summary>The short side of the frame in pixels (720 = 1280×720 at 16:9).</summary>
    public int Resolution { get; init; } = 720;
    public int Fps { get; init; } = 30;
    /// <summary>Subtitles from the script, burned into the frames (and exportable as SRT/VTT).</summary>
    public bool Subtitles { get; init; } = true;
    public List<VideoScene> Scenes { get; init; } = [];
    public List<VideoAudioClip> Audio { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>The frame size: even numbers (what H.264 4:2:0 needs).</summary>
    [JsonIgnore]
    public (int Width, int Height) FrameSize
    {
        get
        {
            int s = Math.Clamp(Resolution, MinHeight, MaxHeight) & ~1;
            int Long() => (int)Math.Round(s * 16 / 9.0) & ~1;
            return Aspect switch { "9:16" => (s, Long()), "1:1" => (s, s), _ => (Long(), s) };
        }
    }

    /// <summary>
    /// One scene per placement of the publication, in the placements' order. Existing scenes keep their IDs and
    /// everything edited in them; a new placement gets a default scene; a scene whose placement left is dropped.
    /// </summary>
    public static PublicationProject Synced(PublicationProject project, Func<PublicationSourceReference, string?>? script = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        var video = project.Video ?? new VideoComposition();
        var byPlacement = new Dictionary<string, VideoScene>(StringComparer.Ordinal);
        foreach (var scene in video.Scenes)
            byPlacement.TryAdd(scene.PlacementId, scene);
        var scenes = project.Sections.Select(p => byPlacement.TryGetValue(p.Id, out var kept)
            ? kept : VideoScene.Default(p.Id, script?.Invoke(p.Source))).ToList();
        if (project.Video is not null && scenes.SequenceEqual(video.Scenes)) return project;
        return project with { Video = video with { Scenes = scenes } };
    }

    public VideoComposition WithScene(VideoScene scene)
    {
        var index = Scenes.FindIndex(s => s.Id == scene.Id);
        if (index < 0) throw new InvalidOperationException("Cena inexistente.");
        var scenes = Scenes.ToList();
        scenes[index] = scene;
        return this with { Scenes = scenes };
    }
}

public sealed record VideoScene
{
    public string Id { get; init; } = "scene_" + Guid.NewGuid().ToString("N");
    /// <summary>The <see cref="PublicationPlacement.Id"/> whose note is this scene's script.</summary>
    public string PlacementId { get; init; } = "";
    public double Duration { get; init; } = 4;
    public VideoTransition Transition { get; init; } = new();
    public string Background { get; init; } = "#101418";
    public List<VideoElement> Elements { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>About 2.6 words a second of narration, at least 3 s: the person changes it in the timeline.</summary>
    public static double SuggestedDuration(string? script)
    {
        var words = VideoScript.Plain(script).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Round(Math.Clamp(words / 2.6, 3, 600) * 2, MidpointRounding.AwayFromZero) / 2;
    }

    public static VideoScene Default(string placementId, string? script = null) => new()
    {
        PlacementId = placementId,
        Duration = SuggestedDuration(script),
        Transition = new VideoTransition { Kind = "fade", Seconds = 0.5 },
        Elements =
        [
            new VideoElement
            {
                Kind = "title", Y = 0.42, Height = 0.3, FontSize = 0.09,
                Keyframes =
                [
                    new VideoKeyframe { Property = "opacity", Time = 0, Value = 0 },
                    new VideoKeyframe { Property = "opacity", Time = 0.6, Value = 1, Easing = "easeOut" },
                    new VideoKeyframe { Property = "y", Time = 0, Value = 0.47 },
                    new VideoKeyframe { Property = "y", Time = 0.6, Value = 0.42, Easing = "easeOut" }
                ]
            }
        ]
    };
}

/// <summary>How a scene enters: "cut" or "fade" (a crossfade over the previous scene, from black at the start).</summary>
public sealed record VideoTransition
{
    public string Kind { get; init; } = "cut";
    public double Seconds { get; init; } = 0.5;
}

/// <summary>
/// An object of a scene. Positions and sizes are fractions of the frame (x, y = centre), font size a fraction of the
/// frame height, times seconds from the scene start. Kinds: title (the scene note's first heading unless Text is set),
/// text, script (the note's text), shape (Text = rect | ellipse), image (Source = a vault image), plot (Text = a
/// gráfico block, e.g. "y = sin(x)") and math (Text = TeX).
/// </summary>
public sealed record VideoElement
{
    public static readonly string[] Kinds = ["title", "text", "script", "shape", "image", "plot", "math"];

    public string Id { get; init; } = "el_" + Guid.NewGuid().ToString("N");
    public string Kind { get; init; } = "text";
    public string? Text { get; init; }
    public string? Source { get; init; }
    public double X { get; init; } = 0.5;
    public double Y { get; init; } = 0.5;
    public double Width { get; init; } = 0.84;
    public double Height { get; init; } = 0.2;
    public string Color { get; init; } = "#ffffff";
    public double FontSize { get; init; } = 0.06;
    public double Start { get; init; }
    public double? End { get; init; }
    public List<VideoKeyframe> Keyframes { get; init; } = [];
    public List<VideoMotion> Motions { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
}

/// <summary>The value a property reaches at a time; <see cref="Easing"/> shapes the way from the previous keyframe.</summary>
public sealed record VideoKeyframe
{
    public static readonly string[] Properties = ["x", "y", "scale", "opacity", "rotation", "progress"];
    public static readonly string[] Easings = ["linear", "easeIn", "easeOut", "easeInOut", "step"];

    public string Property { get; init; } = "opacity";
    public double Time { get; init; }
    public double Value { get; init; }
    public string Easing { get; init; } = "easeInOut";
}

/// <summary>Animation by a function of t (seconds in the scene), added to the property: "0.02*sin(2*pi*t)".</summary>
public sealed record VideoMotion
{
    public string Property { get; init; } = "y";
    public string Expression { get; init; } = "0";
}

/// <summary>A sound file of the vault placed on the timeline: voice, music or effect.</summary>
public sealed record VideoAudioClip
{
    public static readonly string[] Kinds = ["voice", "music", "effect"];

    public string Id { get; init; } = "aud_" + Guid.NewGuid().ToString("N");
    public string Kind { get; init; } = "music";
    public string Path { get; init; } = "";
    /// <summary>When it starts in the video, in seconds.</summary>
    public double Start { get; init; }
    /// <summary>Seconds skipped at the beginning of the file.</summary>
    public double TrimStart { get; init; }
    /// <summary>How long it plays; null = to its end (or the video's).</summary>
    public double? Length { get; init; }
    public double Volume { get; init; } = 1;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
}

/// <summary>Validation of the video part of a publication, called by <see cref="PublicationProjectCodec"/>.</summary>
public static class VideoCompositionRules
{
    public static void Validate(VideoComposition video)
    {
        ArgumentNullException.ThrowIfNull(video);
        static InvalidDataException Bad(string why) => new("Vídeo inválido: " + why);
        if (!VideoComposition.Aspects.Contains(video.Aspect)) throw Bad("proporção " + video.Aspect);
        if (video.Resolution is < VideoComposition.MinHeight or > VideoComposition.MaxHeight) throw Bad("resolução " + video.Resolution);
        if (video.Fps is < 1 or > VideoComposition.MaxFps) throw Bad("quadros por segundo " + video.Fps);
        if (video.Scenes is null || video.Audio is null || video.Scenes.Count > 4096 || video.Audio.Count > 1024) throw Bad("estrutura");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        void Id(string? id) { if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) throw Bad("identidade ausente ou repetida"); }
        static bool Finite(double v) => double.IsFinite(v);
        foreach (var scene in video.Scenes)
        {
            if (scene is null) throw Bad("cena vazia");
            Id(scene.Id);
            if (string.IsNullOrWhiteSpace(scene.PlacementId)) throw Bad("cena sem nota");
            if (!Finite(scene.Duration) || scene.Duration is < 0.1 or > 3600) throw Bad("duração da cena");
            if (scene.Transition is null || scene.Transition.Kind is not ("cut" or "fade") ||
                !Finite(scene.Transition.Seconds) || scene.Transition.Seconds is < 0 or > 10) throw Bad("transição");
            if (scene.Elements is null || scene.Elements.Count > 256) throw Bad("elementos");
            foreach (var e in scene.Elements)
            {
                if (e is null) throw Bad("elemento vazio");
                Id(e.Id);
                if (!VideoElement.Kinds.Contains(e.Kind)) throw Bad("tipo de elemento " + e.Kind);
                if (new[] { e.X, e.Y, e.Width, e.Height, e.FontSize, e.Start }.Any(v => !Finite(v)) || e.End is { } end && !Finite(end) ||
                    e.Width <= 0 || e.Height <= 0 || e.FontSize <= 0 || e.FontSize > 1) throw Bad("geometria do elemento");
                if (e.Kind == "image" && !VideoScript.IsVaultPath(e.Source)) throw Bad("imagem sem caminho do vault");
                if (e.Keyframes is null || e.Motions is null || e.Keyframes.Count > 1024 || e.Motions.Count > 16) throw Bad("animações");
                foreach (var k in e.Keyframes)
                    if (k is null || !VideoKeyframe.Properties.Contains(k.Property) || !VideoKeyframe.Easings.Contains(k.Easing) ||
                        !Finite(k.Time) || !Finite(k.Value)) throw Bad("keyframe");
                foreach (var m in e.Motions)
                {
                    if (m is null || !VideoKeyframe.Properties.Contains(m.Property) || m.Expression is null || m.Expression.Length > 512) throw Bad("movimento");
                    try { FunctionPlot.Expression.Parse(m.Expression, "t"); }
                    catch (FormatException ex) { throw Bad("movimento " + m.Expression + ": " + ex.Message); }
                }
            }
        }
        foreach (var clip in video.Audio)
        {
            if (clip is null) throw Bad("áudio vazio");
            Id(clip.Id);
            if (!VideoAudioClip.Kinds.Contains(clip.Kind) || !VideoScript.IsVaultPath(clip.Path)) throw Bad("áudio " + clip.Path);
            if (!Finite(clip.Start) || !Finite(clip.TrimStart) || !Finite(clip.Volume) || clip.Start < 0 || clip.TrimStart < 0 ||
                clip.Volume is < 0 or > 4 || clip.Length is { } length && (!Finite(length) || length <= 0)) throw Bad("tempo do áudio");
        }
    }
}

/// <summary>The spoken text of a note: what subtitles show and what a scene lasts by default.</summary>
public static class VideoScript
{
    public static bool IsVaultPath(string? path) =>
        !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path) && !path.Contains(':') &&
        !path.Replace('\\', '/').Split('/').Any(part => part is ".." or "") && !path.StartsWith(".urbe", StringComparison.OrdinalIgnoreCase);

    /// <summary>The first heading of the note, or null.</summary>
    public static string? Title(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return null;
        var heading = EmbedFragments.Headings(MarkdownEngine.SplitFrontmatter(markdown).Body).FirstOrDefault();
        return heading is null ? null : Inline(heading);
    }

    /// <summary>Paragraphs and list items without headings, code, comments, images or Markdown marks.</summary>
    public static string Plain(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return "";
        var body = NoteMarks.StripComments(MarkdownEngine.SplitFrontmatter(markdown).Body);
        var lines = body.Replace("\r\n", "\n").Split('\n');
        var kept = new List<string>();
        bool code = false, math = false;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith("```", StringComparison.Ordinal) || line.StartsWith("~~~", StringComparison.Ordinal)) { code = !code; continue; }
            if (line == "$$") { math = !math; continue; }
            if (code || math || line.StartsWith('#') || line.StartsWith('|') || line is "---" or "***") continue;
            line = System.Text.RegularExpressions.Regex.Replace(line, @"^(?:[-*+]\s+(?:\[[ xX]\]\s+)?|\d+[.)]\s+|>\s*)", "");
            line = System.Text.RegularExpressions.Regex.Replace(line, @"\s\^[\w-]+$", "");
            line = Inline(line);
            if (line.Length > 0) kept.Add(line);
        }
        return string.Join("\n", kept);
    }

    private static string Inline(string text)
    {
        text = System.Text.RegularExpressions.Regex.Replace(text, @"!\[\[[^\]]*\]\]|!\[[^\]]*\]\([^)]*\)", "");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\[\[([^\]|#]*)(?:#[^\]|]*)?(?:\|([^\]]*))?\]\]", m => m.Groups[2].Success ? m.Groups[2].Value : m.Groups[1].Value);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\[([^\]]*)\]\([^)]*\)", "$1");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\$([^$]+)\$", "$1");
        text = text.Replace("**", "").Replace("__", "").Replace("~~", "").Replace("==", "").Replace("`", "");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(?<!\w)[*_](\S(?:.*?\S)?)[*_](?!\w)", "$1");
        return System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
    }

    /// <summary>
    /// The script cut into subtitle cues of at most two lines of ~42 characters, spread over the window in proportion
    /// to their length (a reading speed, not a guess of the voice).
    /// </summary>
    public static IReadOnlyList<VideoCue> Cues(string plain, double start, double end)
    {
        var pieces = new List<string>();
        foreach (var sentence in System.Text.RegularExpressions.Regex.Split(plain.Replace('\n', ' '), @"(?<=[.!?…;:])\s+"))
        {
            var words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var current = "";
            foreach (var word in words)
            {
                if (current.Length > 0 && current.Length + 1 + word.Length > 84) { pieces.Add(current); current = word; }
                else current = current.Length == 0 ? word : current + " " + word;
            }
            if (current.Length > 0) pieces.Add(current);
        }
        var cues = new List<VideoCue>();
        if (pieces.Count == 0 || end <= start) return cues;
        double total = pieces.Sum(p => p.Length + 8.0), at = start, span = end - start;
        foreach (var piece in pieces)
        {
            var length = span * (piece.Length + 8) / total;
            cues.Add(new VideoCue(at, Math.Min(end, at + length), piece));
            at += length;
        }
        return cues;
    }

    public static string Srt(IEnumerable<VideoCue> cues) => string.Concat(cues.Select((c, i) =>
        (i + 1).ToString(CultureInfo.InvariantCulture) + "\n" + Stamp(c.Start, ',') + " --> " + Stamp(c.End, ',') + "\n" + c.Text + "\n\n"));

    public static string Vtt(IEnumerable<VideoCue> cues) => "WEBVTT\n\n" + string.Concat(cues.Select(c =>
        Stamp(c.Start, '.') + " --> " + Stamp(c.End, '.') + "\n" + c.Text + "\n\n"));

    private static string Stamp(double seconds, char separator)
    {
        var t = TimeSpan.FromMilliseconds(Math.Round(Math.Max(0, seconds) * 1000));
        return ((int)t.TotalHours).ToString("00", CultureInfo.InvariantCulture) + ":" + t.Minutes.ToString("00", CultureInfo.InvariantCulture) + ":" +
               t.Seconds.ToString("00", CultureInfo.InvariantCulture) + separator + t.Milliseconds.ToString("000", CultureInfo.InvariantCulture);
    }
}

/// <summary>A subtitle: text shown from <see cref="Start"/> to <see cref="End"/> seconds of the video.</summary>
public sealed record VideoCue(double Start, double End, string Text);
