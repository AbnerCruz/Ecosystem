namespace Urbe.Client.Ui;

/// <summary>The kinds of haptic feedback (UX.md P7).</summary>
public enum HapticKind
{
    /// <summary>A light tap: a button pressed.</summary>
    Tick,
    /// <summary>A long press recognised (options open, a drag starts).</summary>
    Hold,
    /// <summary>Something done or dropped in place.</summary>
    Confirm,
    /// <summary>Something refused.</summary>
    Error
}

/// <summary>
/// Haptics of the platform (P7). The Android head sets <see cref="Perform"/> (View.PerformHapticFeedback, which follows
/// the system's touch feedback setting and needs no permission); elsewhere every call is a no-op.
/// </summary>
public static class PlatformHaptics
{
    /// <summary>The platform's haptic feedback (null on a computer).</summary>
    public static Action<HapticKind>? Perform { get; set; }

    /// <summary>Personalisation: off turns every haptic off (P13; on by default).</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>Raised for every request (tests and diagnostics), even without a platform.</summary>
    public static event Action<HapticKind>? Requested;

    public static void Tick() => Fire(HapticKind.Tick);
    public static void Hold() => Fire(HapticKind.Hold);
    public static void Confirm() => Fire(HapticKind.Confirm);
    public static void Error() => Fire(HapticKind.Error);

    public static void Fire(HapticKind kind)
    {
        if (!Enabled) return;
        Requested?.Invoke(kind);
        try { Perform?.Invoke(kind); }
        catch (Exception) { /* feedback is never worth a crash */ }
    }
}
