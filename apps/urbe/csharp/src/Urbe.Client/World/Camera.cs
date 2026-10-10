using Avalonia;

namespace Urbe.Client.World;

/// <summary>app.js camera: world point at the screen centre and zoom (w2s/s2w).</summary>
public sealed class Camera
{
    public const double MinZoom = .22, MaxZoom = 2.8;

    // app.js: camera={x:BASE_W*TILE/2, y:BASE_H*TILE/2, z:1.15}, BASE_W=72, BASE_H=50.
    public double X { get; set; } = 72 * LegacyWorld.Tile / 2.0;
    public double Y { get; set; } = 50 * LegacyWorld.Tile / 2.0;
    public double Zoom { get; private set; } = 1.15;

    public Size Viewport { get; set; }

    public Point WorldToScreen(double x, double y) =>
        new((x - X) * Zoom + Viewport.Width / 2, (y - Y) * Zoom + Viewport.Height / 2);

    public Point ScreenToWorld(Point p) =>
        new((p.X - Viewport.Width / 2) / Zoom + X, (p.Y - Viewport.Height / 2) / Zoom + Y);

    public void SetZoom(double zoom) => Zoom = Math.Clamp(zoom, MinZoom, MaxZoom);

    public void Pan(Vector screenDelta)
    {
        X -= screenDelta.X / Zoom;
        Y -= screenDelta.Y / Zoom;
    }

    /// <summary>Zooms keeping the world point under <paramref name="anchor"/> fixed.</summary>
    public void ZoomAt(Point anchor, double factor)
    {
        var before = ScreenToWorld(anchor);
        Zoom = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
        var after = ScreenToWorld(anchor);
        X += before.X - after.X;
        Y += before.Y - after.Y;
    }

    /// <summary>app.js faixaVisivel(): visible tile range with the original margins.</summary>
    public (int X0, int Y0, int X1, int Y1) VisibleTiles()
    {
        var a = ScreenToWorld(new Point(0, 0));
        var b = ScreenToWorld(new Point(Viewport.Width, Viewport.Height));
        return ((int)Math.Floor(a.X / LegacyWorld.Tile) - 1, (int)Math.Floor(a.Y / LegacyWorld.Tile) - 2,
            (int)Math.Ceiling(b.X / LegacyWorld.Tile) + 1, (int)Math.Ceiling(b.Y / LegacyWorld.Tile) + 2);
    }

    public (int X, int Y) TileAt(Point screen)
    {
        var w = ScreenToWorld(screen);
        return ((int)Math.Floor(w.X / LegacyWorld.Tile), (int)Math.Floor(w.Y / LegacyWorld.Tile));
    }
}
