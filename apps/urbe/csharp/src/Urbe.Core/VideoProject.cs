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
    /// <summary>A finished look for every scene (<see cref="VideoThemes"/>): noite, quadro, papel, neon or sobrio; null = none.</summary>
    public string? Theme { get; init; }
    public List<VideoScene> Scenes { get; init; } = [];
    public List<VideoAudioClip> Audio { get; init; } = [];
    /// <summary>#956: what each scene showed in the last MP4 saved (scene ID → <see cref="VideoTimelineScene.ContentKey"/>).</summary>
    public Dictionary<string, string>? Rendered { get; init; }

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
        // a new video starts with a finished look (the person or the Assistente changes it); old ones keep theirs
        var video = project.Video ?? new VideoComposition { Theme = "noite" };
        var byPlacement = new Dictionary<string, VideoScene>(StringComparer.Ordinal);
        foreach (var scene in video.Scenes)
            byPlacement.TryAdd(scene.PlacementId, scene);
        // scenes written for placements that are not there (another id) go, in order, to the placements without one
        var spare = new Queue<VideoScene>(video.Scenes.Where(s => !project.Sections.Any(p => p.Id == s.PlacementId)));
        var scenes = project.Sections.Select(p => byPlacement.TryGetValue(p.Id, out var kept) ? kept
            : spare.TryDequeue(out var moved) ? moved with { PlacementId = p.Id }
            : VideoScene.Default(p.Id, script?.Invoke(p.Source))).ToList();
        if (project.Video is not null && scenes.SequenceEqual(video.Scenes)) return project;
        return project with { Video = video with { Scenes = scenes } };
    }

    /// <summary>
    /// #956: each scene lasts at least its narration plus <paramref name="pad"/> seconds, so a voice is never cut by
    /// the next scene; a scene longer than its voice keeps the duration the person chose.
    /// </summary>
    public VideoComposition FitToVoices(IReadOnlyDictionary<string, double> voiceSeconds, double pad = 0.4) => this with
    {
        Scenes = Scenes.Select(s => voiceSeconds.TryGetValue(s.Id, out var v) && v + pad > s.Duration
            ? s with { Duration = Math.Min(3600, Math.Ceiling((v + pad) * 10) / 10) } : s).ToList()
    };

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

    /// <summary>A scene for a new placement; its IDs come from the placement, so the same scene is named alike until saved.</summary>
    public static VideoScene Default(string placementId, string? script = null) => new()
    {
        Id = "scene_" + placementId,
        PlacementId = placementId,
        Duration = SuggestedDuration(script),
        Transition = new VideoTransition { Kind = "fade", Seconds = 0.5 },
        Elements =
        [
            new VideoElement
            {
                Id = "title_" + placementId,
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
/// gráfico block, e.g. "y = sin(x)"), math (Text = TeX), path (Text = SVG path data, fitted to the box; progress
/// draws it on) and group (draws nothing: other elements follow it through <see cref="Parent"/>).
/// </summary>
public sealed record VideoElement
{
    public static readonly string[] Kinds = ["title", "text", "script", "shape", "image", "plot", "math", "path", "group"];
    public const int MaxPathData = 8192;

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
    /// <summary>
    /// Another element of the scene this one follows: X and Y become an offset from the parent's centre, and the
    /// parent's scale, rotation and opacity apply to it too. Hidden while the parent is.
    /// </summary>
    public string? Parent { get; init; }
    /// <summary>
    /// #1004: how many instances of this element are drawn (1 to <see cref="VideoProcedural.MaxRepeat"/>); its motions
    /// see i, n and u, so each instance can sit, move and fade on its own. A repeated element is not a parent.
    /// </summary>
    public int Repeat { get; init; } = 1;
    /// <summary>
    /// #1004 slice 2 (kinetic typography): "letters" or "words" draws a text one unit per instance, each in its place in
    /// the text and moved by the motions with i, n and u, as a repeated element; null draws the text whole.
    /// </summary>
    public string? Split { get; init; }
    /// <summary>
    /// #1004 slice 3, effects: glow (a halo of <see cref="GlowColor"/>, or the element's colour), a soft shadow under it
    /// and blur, each a radius as a fraction of the frame's height (0 is off, at most 0.2); <see cref="Gradient"/> paints
    /// the element from its colour (top) to this one (bottom).
    /// </summary>
    public double Glow { get; init; }
    public string? GlowColor { get; init; }
    public double Shadow { get; init; }
    public double Blur { get; init; }
    public string? Gradient { get; init; }

    public static bool IsHexColor(string? c) =>
        c is { Length: 4 or 7 or 9 } && c[0] == '#' && c.Skip(1).All(Uri.IsHexDigit);

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    /// <summary>SVG path data: commands and numbers only, starting with a move.</summary>
    public static bool IsPathData(string? data) =>
        data is { Length: > 0 and <= MaxPathData } && System.Text.RegularExpressions.Regex.IsMatch(data, @"^\s*[Mm][MmLlHhVvCcSsQqTtAaZz0-9eE.,+\-\s]*$");
}

/// <summary>The value a property reaches at a time; <see cref="Easing"/> shapes the way from the previous keyframe.</summary>
public sealed record VideoKeyframe
{
    public static readonly string[] Properties = ["x", "y", "scale", "opacity", "rotation", "progress", "width", "height", "glow", "shadow", "blur"];
    /// <summary>Named curves; any CSS <c>cubic-bezier(x1, y1, x2, y2)</c> is accepted too (x in 0–1). "back" overshoots and settles.</summary>
    public static readonly string[] Easings = ["linear", "easeIn", "easeOut", "easeInOut", "step", "back"];

    public static bool IsEasing(string? easing) => easing is not null && (Easings.Contains(easing) || CubicBezier.TryParse(easing, out _));

    public string Property { get; init; } = "opacity";
    public double Time { get; init; }
    public double Value { get; init; }
    public string Easing { get; init; } = "easeInOut";
}

/// <summary>
/// Animation by a formula of t (seconds in the scene), added to the property: "0.02*sin(2*pi*t)". In a repeated element
/// it also sees i, n and u (<see cref="VideoProcedural"/>); <see cref="Mode"/> "set" makes it the value instead.
/// </summary>
public sealed record VideoMotion
{
    public string Property { get; init; } = "y";
    public string Expression { get; init; } = "0";
    public string Mode { get; init; } = "add";
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
    /// <summary>CST-172: seconds the clip takes to rise from silence at its start and to fall to silence at its end.</summary>
    public double FadeIn { get; init; }
    public double FadeOut { get; init; }

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
        if (video.Theme is not null && VideoThemes.Find(video.Theme) is null)
            throw Bad("tema \"" + video.Theme + "\"; temas: " + string.Join(", ", VideoThemes.All.Select(t => t.Id)));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        void Id(string? id, string what)
        {
            if (string.IsNullOrWhiteSpace(id)) throw Bad(what + " sem id: todo elemento, cena e áudio precisa de um id único");
            if (!ids.Add(id)) throw Bad("o id \"" + id + "\" aparece mais de uma vez (" + what + "); cenas, elementos e áudios precisam de ids diferentes entre si");
        }
        static bool Finite(double v) => double.IsFinite(v);
        foreach (var scene in video.Scenes)
        {
            if (scene is null) throw Bad("cena vazia");
            Id(scene.Id, "cena");
            if (string.IsNullOrWhiteSpace(scene.PlacementId)) throw Bad("cena sem nota");
            if (!Finite(scene.Duration) || scene.Duration is < 0.1 or > 3600) throw Bad("duração da cena");
            if (scene.Transition is null || scene.Transition.Kind is not ("cut" or "fade") ||
                !Finite(scene.Transition.Seconds) || scene.Transition.Seconds is < 0 or > 10) throw Bad("transição");
            if (scene.Elements is null || scene.Elements.Count > 256) throw Bad("elementos");
            foreach (var e in scene.Elements)
            {
                if (e is null) throw Bad("elemento vazio");
                Id(e.Id, "elemento da cena " + scene.Id);
                if (!VideoElement.Kinds.Contains(e.Kind)) throw Bad("tipo de elemento \"" + e.Kind + "\" em " + e.Id + "; tipos: " + string.Join(", ", VideoElement.Kinds));
                if (new[] { e.X, e.Y, e.Width, e.Height, e.FontSize, e.Start }.Any(v => !Finite(v)) || e.End is { } end && !Finite(end) ||
                    e.Width <= 0 || e.Height <= 0 || e.FontSize <= 0 || e.FontSize > 1) throw Bad("geometria do elemento");
                if (e.Kind == "image" && !VideoScript.IsVaultPath(e.Source)) throw Bad("imagem sem caminho do vault");
                if (e.Kind == "path" && !VideoElement.IsPathData(e.Text ?? e.Source)) throw Bad("caminho vetorial de " + e.Id + ": dados de path SVG em text, começando com M");
                if (e.Keyframes is null || e.Motions is null || e.Keyframes.Count > 1024 || e.Motions.Count > 16) throw Bad("animações");
                foreach (var k in e.Keyframes)
                {
                    if (k is null) throw Bad("keyframe vazio em " + e.Id);
                    if (!VideoKeyframe.Properties.Contains(k.Property))
                        throw Bad("keyframe de " + e.Id + " anima \"" + k.Property + "\"; propriedades animáveis: " + string.Join(", ", VideoKeyframe.Properties));
                    if (!VideoKeyframe.IsEasing(k.Easing))
                        throw Bad("keyframe de " + e.Id + " usa a curva \"" + k.Easing + "\"; curvas: " + string.Join(", ", VideoKeyframe.Easings) + " ou cubic-bezier(x1,y1,x2,y2)");
                    if (!Finite(k.Time) || !Finite(k.Value)) throw Bad("keyframe de " + e.Id + " sem tempo ou valor numérico");
                }
                if (e.Split is { } split)
                {
                    if (!VideoProcedural.Splits.Contains(split)) throw Bad("split de " + e.Id + " é \"" + split + "\"; use letters (uma instância por letra) ou words (por palavra)");
                    if (e.Kind is not ("title" or "text")) throw Bad("split de " + e.Id + ": só title e text se dividem em letras ou palavras");
                    if (e.Repeat != 1) throw Bad("split de " + e.Id + ": o número de instâncias é o de letras ou palavras; tire repeat");
                }
                foreach (var (name, v) in new[] { ("glow", e.Glow), ("shadow", e.Shadow), ("blur", e.Blur) })
                    if (!Finite(v) || v < 0 || v > 0.2) throw Bad(name + " de " + e.Id + " é " + v + "; use de 0 (sem) a 0.2 (fração da altura do quadro)");
                foreach (var (name, c) in new[] { ("glowColor", e.GlowColor), ("gradient", e.Gradient) })
                    if (c is not null && !VideoElement.IsHexColor(c)) throw Bad(name + " de " + e.Id + " é \"" + c + "\"; use uma cor #rrggbb");
                if (e.Repeat < 1 || e.Repeat > VideoProcedural.MaxRepeat)
                    throw Bad("repeat de " + e.Id + " é " + e.Repeat + "; de 1 a " + VideoProcedural.MaxRepeat + " instâncias por elemento (mais delas: outro elemento)");
                foreach (var m in e.Motions)
                {
                    if (m is null || !VideoKeyframe.Properties.Contains(m.Property) || m.Expression is null || m.Expression.Length > 512)
                        throw Bad("movimento de " + e.Id + ": propriedade \"" + m?.Property + "\" (animáveis: " + string.Join(", ", VideoKeyframe.Properties) + ") e expressão de até 512 caracteres");
                    if (!VideoProcedural.Modes.Contains(m.Mode)) throw Bad("movimento de " + e.Id + ": mode \"" + m.Mode + "\"; use add (soma ao valor) ou set (vira o valor)");
                    try { VideoProcedural.Check(m.Expression); }
                    catch (FormatException ex) { throw Bad("movimento de " + e.Id + " \"" + m.Expression + "\": " + ex.Message + " (variáveis: t, i, n, u; funções: sin, cos, sqrt, abs, exp, floor, fract, sat, smooth, rand, noise…)"); }
                }
            }
            // a parent is another element of the same scene, and following parents never comes back to the start
            var byId = scene.Elements.ToDictionary(e => e.Id, StringComparer.Ordinal);
            foreach (var e in scene.Elements)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal) { e.Id };
                for (var p = e.Parent; p is not null; p = byId[p].Parent)
                    if (!byId.ContainsKey(p) || !seen.Add(p)) throw Bad("o pai " + p + " de " + e.Id + " não existe na cena ou forma um ciclo");
                if (e.Parent is { } parent && (byId[parent].Repeat > 1 || byId[parent].Split is not null))
                    throw Bad("o pai " + parent + " de " + e.Id + " tem repeat: um elemento repetido não é pai (repita o filho, ou use um grupo como pai)");
            }
        }
        foreach (var clip in video.Audio)
        {
            if (clip is null) throw Bad("áudio vazio");
            Id(clip.Id, "áudio " + clip.Path);
            if (!VideoAudioClip.Kinds.Contains(clip.Kind) || !VideoScript.IsVaultPath(clip.Path)) throw Bad("áudio " + clip.Path);
            if (!Finite(clip.Start) || !Finite(clip.TrimStart) || !Finite(clip.Volume) || clip.Start < 0 || clip.TrimStart < 0 ||
                clip.Volume is < 0 or > 4 || !Finite(clip.FadeIn) || !Finite(clip.FadeOut) || clip.FadeIn < 0 || clip.FadeOut < 0 || clip.Length is { } length && (!Finite(length) || length <= 0)) throw Bad("tempo do áudio");
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
        // the authorship seal is the app's, never shown on screen or spoken
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s*\{ia:""[^""\n]*""\}", "");
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

/// <summary>A CSS timing curve: the cubic Bézier from (0,0) to (1,1) through (x1,y1) and (x2,y2), read as y for a given x.</summary>
public readonly record struct CubicBezier(double X1, double Y1, double X2, double Y2)
{
    private static readonly System.Text.RegularExpressions.Regex Syntax = new(
        @"^\s*cubic-bezier\(\s*([-+]?[\d.]+)\s*,\s*([-+]?[\d.]+)\s*,\s*([-+]?[\d.]+)\s*,\s*([-+]?[\d.]+)\s*\)\s*$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    public static bool TryParse(string text, out CubicBezier curve)
    {
        curve = default;
        var m = Syntax.Match(text);
        if (!m.Success) return false;
        var v = new double[4];
        for (int i = 0; i < 4; i++)
            if (!double.TryParse(m.Groups[i + 1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v[i])) return false;
        // x must stay in 0–1 so the curve is a function of time; y may leave it (overshoot), within reason
        if (v[0] is < 0 or > 1 || v[2] is < 0 or > 1 || v[1] is < -2 or > 3 || v[3] is < -2 or > 3) return false;
        curve = new CubicBezier(v[0], v[1], v[2], v[3]);
        return true;
    }

    private static double At(double a, double b, double s) => 3 * a * s * (1 - s) * (1 - s) + 3 * b * s * s * (1 - s) + s * s * s;

    public double Solve(double x)
    {
        if (x <= 0) return 0;
        if (x >= 1) return 1;
        // the curve's parameter for this x: Newton's method from x, bisection if it does not settle
        double s = x;
        for (int i = 0; i < 8; i++)
        {
            double err = At(X1, X2, s) - x;
            if (Math.Abs(err) < 1e-7) return At(Y1, Y2, s);
            double d = 3 * X1 * (1 - s) * (1 - s) + 6 * (X2 - X1) * s * (1 - s) + 3 * (1 - X2) * s * s;
            if (Math.Abs(d) < 1e-6) break;
            s -= err / d;
        }
        double lo = 0, hi = 1;
        s = x;
        for (int i = 0; i < 60; i++)
        {
            if (At(X1, X2, s) < x) lo = s; else hi = s;
            s = (lo + hi) / 2;
        }
        return At(Y1, Y2, s);
    }
}
