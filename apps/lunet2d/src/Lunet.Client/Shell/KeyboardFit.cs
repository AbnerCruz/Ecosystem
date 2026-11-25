namespace Lunet.Client.Shell;

/// <summary>
/// How much of the bottom of the window the phone's keyboard covers. The rule comes from what the Numa learned on
/// devices (study 25/11, §1): Avalonia does not move the view away from the keyboard by itself; a window the system
/// shrinks for the keyboard (Android up to 14 with adjustResize, not edge-to-edge) is never covered, and adding the
/// keyboard height there pushes the app up and leaves a black band. Only an edge-to-edge window (Android 15+) is covered.
/// </summary>
public static class KeyboardFit
{
    /// <summary>
    /// Switching fields closes and reopens the keyboard: wait this long before giving the space back, so the
    /// screen does not jump down and up again.
    /// </summary>
    public static readonly TimeSpan CloseGrace = TimeSpan.FromMilliseconds(220);

    /// <param name="windowHeight">Client height of the window now.</param>
    /// <param name="keyboardTop">Top of the keyboard in client coordinates, or NaN when it is closed or floating.</param>
    /// <param name="windowShrinks">True when the system already resized the window for the keyboard.</param>
    public static double Cover(double windowHeight, double keyboardTop, bool windowShrinks) =>
        windowShrinks || double.IsNaN(keyboardTop) || windowHeight <= 0 ? 0 : Math.Max(0, windowHeight - keyboardTop);
}
