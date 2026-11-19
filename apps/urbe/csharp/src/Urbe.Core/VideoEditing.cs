namespace Urbe.Core;

/// <summary>
/// #956 P2: editing a scene by touch on the preview — the object under a tap, moving it, a keyframe at the playhead
/// (the "diamond" of mobile editors), size and removal. Each returns a new scene; the panel hands it to the same
/// update (and undo) as the other scene controls.
/// </summary>
public static class VideoEditing
{
    /// <summary>A playhead this close to a keyframe edits that keyframe instead of making a second one beside it.</summary>
    public const double SameTime = 0.1;

    /// <summary>The topmost visible element under (<paramref name="fx"/>, <paramref name="fy"/>), fractions of the frame; scale and rotation counted.</summary>
    public static (VideoLayer Layer, VideoElementState Element)? HitTest(VideoFrame frame, double fx, double fy, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(frame);
        foreach (var layer in frame.Layers.Reverse())
            foreach (var e in layer.Elements.Reverse())
            {
                // a group has no drawing of its own: a tap goes to what is drawn
                if (e.Opacity < 0.05 || e.Scale <= 0 || e.Kind == "group") continue;
                double dx = (fx - e.X) * width, dy = (fy - e.Y) * height, a = -e.Rotation * Math.PI / 180;
                double rx = dx * Math.Cos(a) - dy * Math.Sin(a), ry = dx * Math.Sin(a) + dy * Math.Cos(a);
                if (Math.Abs(rx) <= e.Width * width * e.Scale / 2 && Math.Abs(ry) <= e.Height * height * e.Scale / 2) return (layer, e);
            }
        return null;
    }

    /// <summary>
    /// Puts the element's centre at (x, y) at <paramref name="local"/> seconds of the scene: an element animated in x/y
    /// gets (or changes) a keyframe there; a still one simply moves. An element that follows a parent keeps following
    /// it: the point becomes its offset from the parent (<paramref name="parent"/>, from its state at that instant).
    /// </summary>
    public static VideoScene Move(VideoScene scene, string elementId, double local, double x, double y, VideoParentFrame? parent = null) => Change(scene, elementId, e =>
    {
        x = Math.Clamp(x, -0.5, 1.5);
        y = Math.Clamp(y, -0.5, 1.5);
        if (e.Parent is not null && parent is not null) (x, y) = parent.ToOffset(x, y);
        bool animated = e.Keyframes.Any(k => k.Property is "x" or "y");
        if (!animated) return e with { X = x, Y = y };
        return e with { Keyframes = Set(Set(e.Keyframes, "x", local, x), "y", local, y) };
    });

    /// <summary>The playhead's keyframe of the element: removed when there is one, else position, scale and opacity as they are now.</summary>
    public static VideoScene ToggleKeyframe(VideoScene scene, string elementId, double local, VideoElementState now) => Change(scene, elementId, e =>
    {
        if (HasKeyframe(e, local)) return e with { Keyframes = e.Keyframes.Where(k => Math.Abs(k.Time - local) >= SameTime).ToList() };
        var frames = e.Keyframes;
        // a child's keyframes are its own offset, scale and opacity, not what its parent adds
        var p = e.Parent is null ? null : now.Parent;
        var (x, y) = p?.ToOffset(now.X, now.Y) ?? (now.X, now.Y);
        double scale = now.Scale / Math.Max(1e-6, p?.Scale ?? 1), opacity = Math.Clamp(now.Opacity / Math.Max(1e-6, p?.Opacity ?? 1), 0, 1);
        foreach (var (property, value) in new[] { ("x", x), ("y", y), ("scale", scale), ("opacity", opacity) })
            frames = Set(frames, property, local, value);
        return e with { Keyframes = frames };
    });

    /// <summary>Keyframe times are seconds of the scene, as <see cref="VideoTimeline"/> reads them.</summary>
    public static bool HasKeyframe(VideoElement e, double local) => e.Keyframes.Any(k => Math.Abs(k.Time - local) < SameTime);

    /// <summary>The curves a person chooses by name, in the inspector's order (label, easing).</summary>
    public static readonly (string Label, string Easing)[] Curves =
        [("Suave", "easeInOut"), ("Desacelerar", "easeOut"), ("Acelerar", "easeIn"), ("Constante", "linear"), ("Ressalto", "back"), ("Degrau", "step")];

    /// <summary>The curve that arrives at the keyframes at <paramref name="local"/> (every property keyed there).</summary>
    public static VideoScene SetEasing(VideoScene scene, string elementId, double local, string easing)
    {
        if (!VideoKeyframe.IsEasing(easing)) throw new ArgumentException("Curva desconhecida: " + easing, nameof(easing));
        return Change(scene, elementId, e => e with
        {
            Keyframes = e.Keyframes.Select(k => Math.Abs(k.Time - local) < SameTime ? k with { Easing = easing } : k).ToList()
        });
    }

    /// <summary>The curve of the keyframes at <paramref name="local"/>, or null with none there.</summary>
    public static string? EasingAt(VideoElement e, double local) =>
        e.Keyframes.FirstOrDefault(k => Math.Abs(k.Time - local) < SameTime)?.Easing;

    /// <summary>The box and the text size grow or shrink together.</summary>
    public static VideoScene Resize(VideoScene scene, string elementId, double factor) => Change(scene, elementId, e => e with
    {
        Width = Math.Clamp(e.Width * factor, 0.02, 2),
        Height = Math.Clamp(e.Height * factor, 0.02, 2),
        FontSize = Math.Clamp(e.FontSize * factor, 0.005, 1)
    });

    public static VideoScene Remove(VideoScene scene, string elementId) =>
        scene with { Elements = scene.Elements.Where(e => e.Id != elementId).ToList() };

    private static VideoScene Change(VideoScene scene, string elementId, Func<VideoElement, VideoElement> change)
    {
        var index = scene.Elements.FindIndex(e => e.Id == elementId);
        if (index < 0) throw new InvalidOperationException("Objeto inexistente.");
        var elements = scene.Elements.ToList();
        elements[index] = change(elements[index]);
        return scene with { Elements = elements };
    }

    private static List<VideoKeyframe> Set(List<VideoKeyframe> frames, string property, double time, double value)
    {
        // a keyframe near the playhead keeps its time and easing and takes the new value
        var near = frames.FirstOrDefault(k => k.Property == property && Math.Abs(k.Time - time) < SameTime);
        var kept = frames.Where(k => k != near).ToList();
        kept.Add(near is null ? new VideoKeyframe { Property = property, Time = Math.Max(0, time), Value = value } : near with { Value = value });
        return kept.OrderBy(k => k.Property, StringComparer.Ordinal).ThenBy(k => k.Time).ToList();
    }
}
