using Avalonia;

namespace Urbe.Client.Ui;

/// <summary>
/// CST-043: where the items of the radial menu sit and which one a drag points to. The trigger is in the bottom
/// right corner, so the items go on the quarter arc from straight up (90°) to straight left (180°), the part of the
/// circle the thumb reaches (ArchMenu/ThumbMenu); the places never change, so the hand learns them (marking menus).
/// Offsets are in screen coordinates from the trigger's centre (y grows downwards).
/// </summary>
public static class RadialLayout
{
    public const double Start = 90, End = 180;

    /// <summary>The angle of item <paramref name="index"/> of <paramref name="count"/>, in degrees (0° = right, 90° = up).</summary>
    public static double Angle(int index, int count) =>
        count <= 1 ? (Start + End) / 2 : Start + (End - Start) * index / (count - 1);

    public static IReadOnlyList<Point> Positions(int count, double radius)
    {
        var points = new Point[Math.Max(0, count)];
        for (int i = 0; i < points.Length; i++)
        {
            double a = Angle(i, count) * Math.PI / 180;
            points[i] = new Point(Clean(radius * Math.Cos(a)), Clean(-radius * Math.Sin(a)));
        }
        return points;
    }

    /// <summary>
    /// The item a drag of (<paramref name="dx"/>, <paramref name="dy"/>) from the trigger's centre points to: the one
    /// nearest in angle; -1 inside the dead zone (a tap), past <paramref name="maxDistance"/> (dragged away: cancel) or
    /// away from the arc by more than half the gap between items.
    /// </summary>
    public static int Pick(double dx, double dy, int count, double deadZone, double maxDistance = double.PositiveInfinity)
    {
        double d2 = dx * dx + dy * dy;
        if (count <= 0 || d2 < deadZone * deadZone || d2 > maxDistance * maxDistance) return -1;
        double angle = Math.Atan2(-dy, dx) * 180 / Math.PI;
        if (angle < 0) angle += 360;
        double half = count <= 1 ? (End - Start) / 2 : (End - Start) / (count - 1) / 2;
        if (angle < Start - half || angle > End + half) return -1;
        int best = -1;
        double nearest = double.MaxValue;
        for (int i = 0; i < count; i++)
        {
            double d = Math.Abs(angle - Angle(i, count));
            if (d < nearest) { nearest = d; best = i; }
        }
        return best;
    }

    // cos(90°) is 6e-17, not 0
    private static double Clean(double v) => Math.Abs(v) < 1e-9 ? 0 : v;
}
