using Avalonia;
using Avalonia.Input;

namespace Lunet.Client;

/// <summary>
/// Tells a tap from a drag on touch: a finger that moved more than <see cref="Slop"/> (Android's touch slop is ~8 dp)
/// between press and release was scrolling, and must not place the caret's keyboard on screen. One record per pointer,
/// kept by the shell from the press (tunnel, before any control handles it).
/// </summary>
public static class TouchTap
{
    public const double Slop = 10;
    private static readonly Dictionary<int, (Point At, long Ticks)> Pressed = [];

    /// <summary>A press older than this is forgotten (a release Android cancelled must not block the keyboard).</summary>
    public static readonly TimeSpan Expiry = TimeSpan.FromSeconds(2);

    private static void Prune()
    {
        var now = Environment.TickCount64;
        foreach (var id in Pressed.Where(p => now - p.Value.Ticks > Expiry.TotalMilliseconds).Select(p => p.Key).ToArray()) Pressed.Remove(id);
    }

    /// <summary>Records where a finger went down (relative to the window).</summary>
    public static void Press(PointerPressedEventArgs e)
    {
        Prune();
        if (e.Pointer.Type != PointerType.Mouse) Pressed[e.Pointer.Id] = (e.GetPosition(null), Environment.TickCount64);
    }


    /// <summary>True when the release ends a tap (or a mouse click); false when the finger dragged (a scroll).</summary>
    /// <summary>The release in progress ends a drag (a scroll), not a tap. A mouse never drags here.</summary>
    public static bool Moved(PointerReleasedEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse || !Pressed.TryGetValue(e.Pointer.Id, out var press)) return false;
        var start = press.At;
        var end = e.GetPosition(null);
        return Math.Abs(end.X - start.X) > Slop || Math.Abs(end.Y - start.Y) > Slop;
    }

    /// <summary>The finger left the screen (the shell calls it last, after every control saw the release).</summary>
    public static void Release(PointerReleasedEventArgs e) => Pressed.Remove(e.Pointer.Id);

    /// <summary>The touch was cancelled (capture lost): it is no longer down.</summary>
    public static void Cancel(IPointer pointer) => Pressed.Remove(pointer.Id);
}
