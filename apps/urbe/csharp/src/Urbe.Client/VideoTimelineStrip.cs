using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// #956 P2: the video's timeline as a strip — scenes as blocks as long as they last, the sound tracks under them, the
/// playhead and the selected object's keyframes. A tap goes to that instant; dragging a scene's right edge changes
/// how long it lasts (written when the finger leaves, through the same update and undo as the other controls).
/// </summary>
public sealed class VideoTimelineStrip : Control
{
    public const double SceneTrack = 44, AudioTrack = 14, Gap = 4, EdgeGrab = 14, MinDuration = 0.5;
    private VideoTimeline? _timeline;
    private double _time;
    private IReadOnlyList<double> _keyframes = [];
    private (VideoTimelineScene Scene, double Duration)? _resizing;

    public VideoTimelineStrip()
    {
        Height = SceneTrack + Gap + AudioTrack + 6;
        AutomationProperties.SetName(this, "Linha do tempo do vídeo");
        PointerPressed += (_, e) => { if (Press(e.GetPosition(this).X)) e.Pointer.Capture(this); e.Handled = true; };
        PointerMoved += (_, e) => Drag(e.GetPosition(this).X);
        PointerReleased += (_, _) => Release();
        PointerCaptureLost += (_, _) => Release();
    }

    /// <summary>A tap asked to go to this time (seconds of the video).</summary>
    public event Action<double>? Seek;
    /// <summary>A scene's right edge was dragged: the scene with its new duration.</summary>
    public event Action<VideoScene>? SceneResized;

    public void Show(VideoTimeline timeline, double time, IReadOnlyList<double> keyframes)
    {
        _timeline = timeline;
        _time = time;
        _keyframes = keyframes;
        InvalidateVisual();
    }

    private double Duration => Math.Max(0.001, _timeline?.Duration ?? 1);
    // room after the end, so the last scene's edge can be pulled to make the video longer
    private double Span => Duration * 1.15;
    public double TimeAt(double x) => Math.Max(0, x / Math.Max(1, Bounds.Width) * Span);
    public double X(double t) => t / Span * Bounds.Width;

    /// <summary>The scene whose right edge is under <paramref name="x"/> (within a finger's width), or null.</summary>
    public VideoTimelineScene? EdgeAt(double x) =>
        _timeline?.Scenes.Where(s => Math.Abs(X(s.End) - x) <= EdgeGrab).OrderBy(s => Math.Abs(X(s.End) - x)).FirstOrDefault();

    /// <summary>True when a drag of an edge starts (the strip keeps the pointer).</summary>
    public bool Press(double x)
    {
        if (_timeline is null) return false;
        if (EdgeAt(x) is { } scene) { _resizing = (scene, scene.Duration); return true; }
        Seek?.Invoke(Math.Min(TimeAt(x), Duration));
        return false;
    }

    public void Drag(double x)
    {
        if (_resizing is not { } r) return;
        // tenths of a second, never shorter than half a second
        var duration = Math.Max(MinDuration, Math.Round((TimeAt(x) - r.Scene.Start) * 10) / 10);
        _resizing = (r.Scene, duration);
        InvalidateVisual();
    }

    public void Release()
    {
        if (_resizing is not { } r) return;
        _resizing = null;
        if (Math.Abs(r.Duration - r.Scene.Duration) >= 0.05) SceneResized?.Invoke(r.Scene.Scene with { Duration = r.Duration });
        else InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (_timeline is not { } timeline || Bounds.Width <= 0) return;
        var accent = UrbeTheme.Brush(UrbeTheme.Accent);
        var text = UrbeTheme.Brush(UrbeTheme.Text);
        var shades = new[] { new SolidColorBrush(Color.FromRgb(0x33, 0x40, 0x5c)), new SolidColorBrush(Color.FromRgb(0x24, 0x2c, 0x3d)) };
        var outline = new Pen(new SolidColorBrush(Color.FromRgb(0x10, 0x14, 0x18)), 2);
        for (int i = 0; i < timeline.Scenes.Count; i++)
        {
            var s = timeline.Scenes[i];
            double end = _resizing is { } r && r.Scene.Scene.Id == s.Scene.Id ? s.Start + r.Duration : s.End;
            var box = new Rect(X(s.Start) + 1, 0, Math.Max(2, X(end) - X(s.Start) - 2), SceneTrack);
            context.DrawRectangle(shades[i % 2], outline, box, 6, 6);
            var label = new FormattedText((i + 1) + " " + (s.Title ?? "") + " · " + (end - s.Start).ToString("0.#", CultureInfo.GetCultureInfo("pt-BR")) + " s",
                CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 11, text) { MaxTextWidth = Math.Max(1, box.Width - 10), MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
            context.DrawText(label, new Point(box.X + 6, box.Y + (SceneTrack - label.Height) / 2));
        }
        // the edges a finger drags, over the blocks (a fade makes the next scene cover the end of the previous one)
        foreach (var s in timeline.Scenes)
        {
            double end = _resizing is { } r && r.Scene.Scene.Id == s.Scene.Id ? s.Start + r.Duration : s.End;
            context.DrawRectangle(_resizing?.Scene.Scene.Id == s.Scene.Id ? accent : text, null, new Rect(X(end) - 2, 9, 4, SceneTrack - 18), 2, 2);
        }
        foreach (var clip in timeline.Audio)
        {
            double length = clip.Length ?? Math.Max(0, timeline.Duration - clip.Start);
            var brush = clip.Kind == "voice" ? accent : new SolidColorBrush(Color.FromRgb(0x6b, 0x8f, 0x71));
            context.DrawRectangle(brush, null, new Rect(X(clip.Start), SceneTrack + Gap, Math.Max(2, X(clip.Start + length) - X(clip.Start)), AudioTrack), 3, 3);
        }
        foreach (var k in _keyframes)
        {
            var c = new Point(X(k), SceneTrack - 7);
            var diamond = new PolylineGeometry([new(c.X, c.Y - 5), new(c.X + 5, c.Y), new(c.X, c.Y + 5), new(c.X - 5, c.Y)], true);
            context.DrawGeometry(Brushes.White, null, diamond);
        }
        context.DrawRectangle(Brushes.White, null, new Rect(X(_time) - 1, 0, 2, Bounds.Height));
    }
}
