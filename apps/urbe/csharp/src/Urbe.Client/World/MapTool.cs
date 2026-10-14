using Avalonia;
using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client.World;

/// <summary>
/// A modal tool of the city canvas (app.js tool = 'region' / 'house' + v21Placement): while it is
/// active a one-finger drag does not pan the camera, it drives the tool. Pinch and wheel still zoom.
/// </summary>
public abstract class MapTool
{
    public abstract void Down(int tx, int ty);
    public abstract void Move(int tx, int ty);
    public abstract void Up(int tx, int ty);
    /// <summary>A second finger arrived (pinch) or the gesture was lost: forget the draft.</summary>
    public virtual void Cancel() { }
    public abstract void Render(DrawingContext context, WorldView view);
}

/// <summary>tool 'region': drag a rectangle (regionDraft), reported when the finger lifts.</summary>
public sealed class RegionDrawTool(Action<int, int, int, int> finished) : MapTool
{
    private (int X, int Y)? _a, _b;

    public (int X, int Y, int W, int H)? Draft => _a is { } a && _b is { } b
        ? (Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X) + 1, Math.Abs(a.Y - b.Y) + 1) : null;

    public override void Down(int tx, int ty) { _a = _b = (tx, ty); }
    public override void Move(int tx, int ty) { if (_a is not null) _b = (tx, ty); }
    public override void Up(int tx, int ty)
    {
        if (_a is null) return;
        _b = (tx, ty);
        var d = Draft!.Value;
        _a = _b = null;
        finished(d.X, d.Y, d.W, d.H);
    }
    public override void Cancel() { _a = _b = null; }

    public override void Render(DrawingContext context, WorldView view)
    {
        if (Draft is not { } d) return;
        var p = view.Camera.WorldToScreen(d.X * LegacyWorld.Tile, d.Y * LegacyWorld.Tile);
        double z = view.Camera.Zoom, size = LegacyWorld.Tile * z;
        var pen = new Pen(UrbeTheme.Brush("#9fe4ff"), 1, new DashStyle([6, 4], 0));
        context.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x22, 0x61, 0xc8, 0xff)), pen, new Rect(p.X, p.Y, d.W * size, d.H * size));
    }
}

/// <summary>v21Placement 'new': a 3×3 ghost house follows the finger; the candidate tile is read by the dock.</summary>
public sealed class PlacementTool(Func<int, int, bool> valid, Action<(int X, int Y)?> changed) : MapTool
{
    public (int X, int Y)? Candidate { get; private set; }

    private void Set(int tx, int ty) { Candidate = (tx, ty); changed(Candidate); }
    public override void Down(int tx, int ty) => Set(tx, ty);
    public override void Move(int tx, int ty) => Set(tx, ty);
    public override void Up(int tx, int ty) => Set(tx, ty);

    public override void Render(DrawingContext context, WorldView view)
    {
        if (Candidate is not { } c) return;
        var p = view.Camera.WorldToScreen(c.X * LegacyWorld.Tile, c.Y * LegacyWorld.Tile);
        double z = view.Camera.Zoom, size = LegacyWorld.Tile * z;
        bool ok = valid(c.X, c.Y);
        if (view.GhostArt(c.X, c.Y) is { } art)
        {
            double s = LegacyWorld.Tile * z / LegacyWorld.TilePixels, w = 48 * s, h = 56 * s;
            using (context.PushOpacity(.62))
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = Avalonia.Media.Imaging.BitmapInterpolationMode.None }))
                context.DrawImage(art, new Rect(0, 0, LegacyWorldBuildings.Width, LegacyWorldBuildings.Height),
                    new Rect(Math.Round(p.X), Math.Round(p.Y + 3 * size - h), Math.Ceiling(w), Math.Ceiling(h)));
        }
        context.DrawRectangle(null, new Pen(UrbeTheme.Brush(ok ? "#7ef29a" : "#ef766f"), 3), new Rect(p.X, p.Y, 3 * size, 3 * size));
    }
}

/// <summary>
/// Placing a decoration (DEC-0045, UC-41): its sprite follows the finger, half transparent, framed in green where it fits
/// and in red where it does not (water, a building, a street or another decoration).
/// </summary>
public sealed class DecorationTool(WorldDecorations.Kind kind, Func<int, int, bool> valid, Action<(int X, int Y)?> changed) : MapTool
{
    public WorldDecorations.Kind Kind { get; } = kind;
    public (int X, int Y)? Candidate { get; private set; }

    private void Set(int tx, int ty) { Candidate = (tx, ty); changed(Candidate); }
    public override void Down(int tx, int ty) => Set(tx, ty);
    public override void Move(int tx, int ty) => Set(tx, ty);
    public override void Up(int tx, int ty) => Set(tx, ty);

    public override void Render(DrawingContext context, WorldView view)
    {
        if (Candidate is not { } c) return;
        var rect = view.DecorationRect(Kind, c.X, c.Y);
        if (view.DecorationArt(Kind.Id) is { } art)
            using (context.PushOpacity(.7))
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = Avalonia.Media.Imaging.BitmapInterpolationMode.None }))
                context.DrawImage(art, new Rect(0, 0, art.Size.Width, art.Size.Height), rect);
        context.DrawRectangle(null, new Pen(UrbeTheme.Brush(valid(c.X, c.Y) ? "#7ef29a" : "#ef766f"), 3), rect);
    }
}
