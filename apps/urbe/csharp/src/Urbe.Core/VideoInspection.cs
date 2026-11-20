using System.Globalization;

namespace Urbe.Core;

/// <summary>A visual problem of a video at a time: which scene and element, what and how to fix it.</summary>
public sealed record VideoIssue(ReviewSeverity Severity, int Scene, string SceneId, string? ElementId, double Time, string Message, string Fix)
{
    public string Place => "cena " + (Scene + 1) + " (" + SceneId + ")" + (ElementId is null ? "" : ", elemento " + ElementId) +
                           ", " + Time.ToString("0.0#", CultureInfo.GetCultureInfo("pt-BR")) + " s";
}

/// <summary>How a text element fits its box when drawn: the size it was reduced to (1 = as set) and whether it ended with "…".</summary>
public readonly record struct VideoTextFit(double Scale, bool Cut);

/// <summary>
/// #956: the objective checks of a video's look, over the same evaluated frames the preview and the MP4 use — an
/// object out of the frame or of the title-safe area (SMPTE ST 2046-1: 90 % of the frame), text that does not fit its
/// box (measured by the renderer, when given), text over text or under the subtitle, contrast below WCAG 2 (4.5:1,
/// 3:1 for titles) and images missing from the vault. Taste (rhythm, beauty) is not judged here.
/// </summary>
public static class VideoInspection
{
    public const double SafeMargin = 0.05;
    private static readonly HashSet<string> Visual = ["title", "text", "script", "math", "plot", "image"];
    private static readonly HashSet<string> Text = ["title", "text", "script"];

    public static IReadOnlyList<VideoIssue> Run(VideoTimeline timeline, Func<VideoElementState, VideoTextFit>? fit = null, Func<string, bool>? fileExists = null)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        var issues = new List<VideoIssue>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        double W = timeline.Width, H = timeline.Height;
        for (int s = 0; s < timeline.Scenes.Count; s++)
        {
            var scene = timeline.Scenes[s];
            var id = scene.Scene.Id;
            // each problem once per element, at the first time it shows
            void Add(ReviewSeverity severity, string? element, string kind, double t, string message, string fix)
            {
                if (seen.Add(id + "|" + element + "|" + kind)) issues.Add(new(severity, s, id, element, t, message, fix));
            }
            foreach (var e in scene.Elements)
            {
                if (e.Start >= scene.Duration) Add(ReviewSeverity.Warning, e.Id, "never", e.Start, "Começa em " + N(e.Start) + " s, depois do fim da cena (" + N(scene.Duration) + " s): nunca aparece.", "Diminua start ou aumente a duração da cena.");
                if (e.Kind == "image" && fileExists is not null && e.Source is { } src && !fileExists(src))
                    Add(ReviewSeverity.Error, e.Id, "image", e.Start, "Imagem não encontrada no vault: " + src + ".", "Corrija source com o caminho de uma imagem do vault.");
            }
            foreach (var local in Samples(scene))
            {
                var t = scene.Start + local;
                var frame = timeline.Evaluate(t);
                if (frame.Layers.FirstOrDefault(l => ReferenceEquals(l.Scene, scene)) is not { } layer) continue;
                // #1004 slice 4: measured where the camera shows them
                var shown = layer.Camera is { } cam ? layer.Elements.Select(e => cam.Apply(e, timeline.Width, timeline.Height)) : layer.Elements;
                var visible = shown.Where(e => e.Opacity >= 0.1 && e.Scale > 0).ToList();
                bool subtitle = frame.Subtitle is not null;
                for (int i = 0; i < visible.Count; i++)
                {
                    var e = visible[i];
                    if (!Visual.Contains(e.Kind)) continue;
                    // a split text's units scale and turn about their own centres; the text's box is the one laid out
                    var b = Box(e.Split is not null ? e with { Scale = 1, Rotation = 0 } : e, W, H);
                    // #1004: instances of a repeated shape are a field (particles, stars, a grid) that may run past the
                    // edges on purpose; a repeated text is still read, so it is still checked
                    bool field = e.Instance >= 0 && !Text.Contains(e.Kind);
                    if (field) { }
                    else if (b.L < -0.01 * W || b.T < -0.01 * H || b.R > 1.01 * W || b.B > 1.01 * H)
                        Add(ReviewSeverity.Error, e.Id, "out", local, Name(e) + " sai do quadro.", "Aproxime x/y do centro ou reduza width/height/scale (posições são do centro, de 0 a 1).");
                    else if (b.L < SafeMargin * W || b.T < SafeMargin * H || b.R > (1 - SafeMargin) * W || b.B > (1 - SafeMargin) * H)
                        Add(ReviewSeverity.Warning, e.Id, "safe", local, Name(e) + " encosta na borda (fora dos 90 % seguros do quadro); pode ser cortado em telas e players.", "Mantenha a caixa entre 0,05 e 0,95 do quadro.");
                    // a split text is one text: measured once, by its first unit
                    if (e.Split is not null && e.Instance > 0) continue;
                    if (Text.Contains(e.Kind) && fit is not null && e.Text.Trim().Length > 0 && fit(e) is var f)
                    {
                        if (f.Cut) Add(ReviewSeverity.Error, e.Id, "cut", local, Name(e) + " não cabe na caixa: o texto termina em reticências.", "Aumente width/height do elemento, diminua fontSize ou encurte o texto.");
                        else if (f.Scale < 0.5) Add(ReviewSeverity.Error, e.Id, "shrunk", local, Name(e) + " fica pequeno demais para ler: só cabe a " + Math.Round(f.Scale * 100) + " % do tamanho pedido.", "Aumente width/height do elemento ou encurte o texto.");
                        else if (f.Scale < 0.7) Add(ReviewSeverity.Warning, e.Id, "shrunk", local, Name(e) + " só cabe reduzido a " + Math.Round(f.Scale * 100) + " % do tamanho pedido.", "Aumente a caixa ou diminua fontSize para o tamanho real.");
                    }
                    if (Text.Contains(e.Kind) && Contrast(e, visible.Take(i), timeline.Theme is { } th && string.Equals(layer.Scene.Scene.Background, VideoThemes.DefaultBackground, StringComparison.OrdinalIgnoreCase)
                        ? th.Bottom : layer.Scene.Scene.Background) is { } ratio && ratio < (e.Kind == "title" ? 3 : 4.5))
                        Add(ReviewSeverity.Warning, e.Id, "contrast", local, Name(e) + " tem contraste " + ratio.ToString("0.0", CultureInfo.GetCultureInfo("pt-BR")) + ":1 com o fundo, abaixo do legível (WCAG: " + (e.Kind == "title" ? "3" : "4,5") + ":1).", "Troque color ou o fundo por cores mais distantes em claridade.");
                    if (subtitle && Text.Contains(e.Kind) && Intersection(b, (0.07 * W, 0.80 * H, 0.93 * W, 0.95 * H)) > 0.02 * Area(b))
                        Add(ReviewSeverity.Warning, e.Id, "subtitle", local, Name(e) + " fica embaixo da legenda.", "Suba o elemento (y abaixo de 0,75) ou desligue subtitles.");
                    for (int j = 0; j < i; j++)
                    {
                        var o = visible[j];
                        if (o.Id == e.Id && e.Split is not null) continue;   // the letters of one text share its box
                        if (!Visual.Contains(o.Kind) || !(Text.Contains(e.Kind) || Text.Contains(o.Kind))) continue;
                        var c = Box(o, W, H);
                        if (Intersection(b, c) > 0.05 * Math.Min(Area(b), Area(c)))
                            Add(ReviewSeverity.Warning, e.Id + "+" + o.Id, "overlap", local, Name(e) + " fica por cima d" + Name(o, true) + ".", "Separe as caixas (x/y/width/height) ou faça um sair antes de o outro entrar (start/end).");
                    }
                }
            }
        }
        // a voice that goes on past the end of the scene it starts in is talked over by the next one
        foreach (var clip in timeline.Audio.Where(a => a.Kind == "voice" && a.Length is not null))
        {
            int s = timeline.Scenes.ToList().FindLastIndex(x => clip.Start >= x.Start - 0.05);
            if (s < 0) continue;
            var scene = timeline.Scenes[s];
            var over = clip.Start + clip.Length!.Value - scene.End;
            if (over > 0.05 && s < timeline.Scenes.Count - 1)
                issues.Add(new(ReviewSeverity.Warning, s, scene.Scene.Id, null, clip.Start - scene.Start,
                    "A narração " + clip.Path + " dura " + N(clip.Length.Value) + " s e passa " + N(over) + " s do fim da cena: a próxima cena entra durante a fala.",
                    "Aumente a duração da cena para " + N(Math.Ceiling((clip.Start - scene.Start + clip.Length.Value + 0.4) * 10) / 10) + " s ou narre de novo (a narração ajusta as cenas)."));
        }
        return issues.OrderBy(i => i.Severity).ThenBy(i => i.Scene).ThenBy(i => i.Time).ToArray();
    }

    /// <summary>The instants a scene is looked at: when its entrance ends, every keyframe, element start and end, and every half second.</summary>
    private static IEnumerable<double> Samples(VideoTimelineScene scene)
    {
        var d = scene.Duration;
        var times = new SortedSet<double> { Math.Min(scene.FadeIn, d * 0.99), d * 0.99 };
        for (double t = 0.5; t < d; t += 0.5) times.Add(t);
        foreach (var e in scene.Elements)
        {
            times.Add(e.Start);
            if (e.End is { } end) times.Add(end - 0.01);
            foreach (var k in e.Keyframes) times.Add(k.Time);
        }
        return times.Where(t => t >= 0 && t < d);
    }

    /// <summary>"O título el_1"; lowercase after a preposition ("por cima d" + "o título" = "do título").</summary>
    private static string Name(VideoElementState e, bool lower = false)
    {
        var name = e.Kind switch
        {
            "title" => "O título", "script" => "O roteiro", "math" => "A fórmula", "plot" => "O gráfico", "image" => "A imagem", _ => "O texto"
        } + " " + e.Id;
        return lower ? char.ToLowerInvariant(name[0]) + name[1..] : name;
    }

    /// <summary>The element's box in pixels, scaled and turned (the box around the turned box).</summary>
    private static (double L, double T, double R, double B) Box(VideoElementState e, double W, double H)
    {
        double w = e.Width * W * e.Scale, h = e.Height * H * e.Scale, a = e.Rotation * Math.PI / 180;
        double bw = Math.Abs(w * Math.Cos(a)) + Math.Abs(h * Math.Sin(a)), bh = Math.Abs(w * Math.Sin(a)) + Math.Abs(h * Math.Cos(a));
        return (e.X * W - bw / 2, e.Y * H - bh / 2, e.X * W + bw / 2, e.Y * H + bh / 2);
    }

    private static double Area((double L, double T, double R, double B) b) => Math.Max(0, b.R - b.L) * Math.Max(0, b.B - b.T);

    private static double Intersection((double L, double T, double R, double B) a, (double L, double T, double R, double B) b) =>
        Area((Math.Max(a.L, b.L), Math.Max(a.T, b.T), Math.Min(a.R, b.R), Math.Min(a.B, b.B)));

    /// <summary>The text color against what is behind its centre: the last shape under it, else the scene; null over an image.</summary>
    private static double? Contrast(VideoElementState e, IEnumerable<VideoElementState> below, string background)
    {
        string back = background;
        foreach (var o in below)
        {
            if (o.Kind is not ("shape" or "image")) continue;
            if (Math.Abs(o.X - e.X) > o.Width * o.Scale / 2 || Math.Abs(o.Y - e.Y) > o.Height * o.Scale / 2) continue;
            if (o.Kind == "image") return null;
            // a translucent shape is mixed with what is behind it, as it is drawn
            back = o.Opacity >= 0.999 ? o.Color : Blend(o.Color, back, o.Opacity) ?? o.Color;
        }
        return Luminance(e.Color) is { } fg && Luminance(back) is { } bg ? (Math.Max(fg, bg) + 0.05) / (Math.Min(fg, bg) + 0.05) : null;
    }

    /// <summary><paramref name="top"/> over <paramref name="under"/> at <paramref name="alpha"/>, as #rrggbb.</summary>
    private static string? Blend(string top, string under, double alpha)
    {
        static int[]? Rgb(string c)
        {
            var hex = c.Trim().TrimStart('#');
            if (hex.Length == 3) hex = string.Concat(hex.Select(ch => new string(ch, 2)));
            if (hex.Length == 8) hex = hex[2..];
            return hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? [v >> 16 & 255, v >> 8 & 255, v & 255] : null;
        }
        if (Rgb(top) is not { } a || Rgb(under) is not { } b) return null;
        alpha = Math.Clamp(alpha, 0, 1);
        return "#" + string.Concat(a.Zip(b, (x, y) => ((int)Math.Round(x * alpha + y * (1 - alpha))).ToString("x2", CultureInfo.InvariantCulture)));
    }

    /// <summary>WCAG 2 relative luminance of #rgb / #rrggbb (#aarrggbb takes the colour part).</summary>
    public static double? Luminance(string? color)
    {
        var hex = color?.Trim().TrimStart('#') ?? "";
        if (hex.Length == 3) hex = string.Concat(hex.Select(c => new string(c, 2)));
        if (hex.Length == 8) hex = hex[2..];
        if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return null;
        static double C(int c) { var x = c / 255.0; return x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }
        return 0.2126 * C(v >> 16 & 255) + 0.7152 * C(v >> 8 & 255) + 0.0722 * C(v & 255);
    }

    private static string N(double v) => v.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"));
}
