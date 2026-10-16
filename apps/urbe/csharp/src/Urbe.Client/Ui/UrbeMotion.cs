using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using M = Urbe.Client.UrbeTokens.Motion;

namespace Urbe.Client.Ui;

/// <summary>
/// Overlay transitions and touch feedback (UX.md P1, P6): fade + slide in/out with the motion tokens, done with
/// Avalonia transitions (composition-driven, nothing on the touch path waits). IsVisible keeps its meaning: an
/// overlay is visible the moment it is shown, and only <see cref="HideAsync"/> waits for its leave animation.
/// </summary>
public static class UrbeMotion
{
    private static readonly AttachedProperty<int> Generation = AvaloniaProperty.RegisterAttached<Control, int>("UrbeMotionGeneration", typeof(UrbeMotion));

    private static bool Animate(bool? force) => force ?? !M.Reduced;

    private static ITransform Shift(double dx, double dy) =>
        TransformOperations.Parse(FormattableString.Invariant($"translate({dx}px, {dy}px)"));

    private static void UseTransitions(Control c, TimeSpan duration, Avalonia.Animation.Easings.Easing easing)
    {
        c.Transitions =
        [
            new DoubleTransition { Property = Visual.OpacityProperty, Duration = duration, Easing = easing },
            new TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = duration, Easing = easing }
        ];
    }

    /// <summary>
    /// Fades and slides a control in from (dx, dy) to its place in <see cref="M.Normal"/> with ease-out. Instant when
    /// motion is reduced (or <paramref name="animate"/> is false).
    /// </summary>
    public static void Enter(Control c, double dx = 0, double dy = 12, TimeSpan? duration = null, bool? animate = null)
    {
        c.SetValue(Generation, c.GetValue(Generation) + 1);
        c.Transitions = null;
        if (!Animate(animate))
        {
            c.Opacity = 1;
            c.RenderTransform = null;
            return;
        }
        c.Opacity = 0;
        c.RenderTransform = Shift(dx, dy);
        UseTransitions(c, duration ?? M.Normal, M.EaseOut);
        c.Opacity = 1;
        c.RenderTransform = Shift(0, 0);
    }

    /// <summary>
    /// Fades and slides a control out to (dx, dy) in <see cref="M.Fast"/>, then hides it (IsVisible = false) and puts
    /// it back in place for the next show. A show in the meantime wins.
    /// </summary>
    public static async Task HideAsync(Control c, double dx = 0, double dy = 8, TimeSpan? duration = null, bool? animate = null)
    {
        int gen = c.GetValue(Generation) + 1;
        c.SetValue(Generation, gen);
        if (!Animate(animate) || !c.IsVisible)
        {
            c.Transitions = null;
            c.IsVisible = false;
            c.Opacity = 1;
            c.RenderTransform = null;
            return;
        }
        var time = duration ?? M.Fast;
        c.Transitions = null;
        if (c.RenderTransform is null) c.RenderTransform = Shift(0, 0);
        UseTransitions(c, time, M.EaseIn);
        c.Opacity = 0;
        c.RenderTransform = Shift(dx, dy);
        await Task.Delay(time);
        if (c.GetValue(Generation) != gen) return;   // shown again meanwhile
        c.Transitions = null;
        c.IsVisible = false;
        c.Opacity = 1;
        c.RenderTransform = null;
    }

    /// <summary>
    /// Every time the control becomes visible it enters with <see cref="Enter"/> (for overlays that are shown by
    /// setting IsVisible in many places: the editor, the Explorer).
    /// </summary>
    public static void EnterOnShow(Control c, double dx = 0, double dy = 12)
    {
        c.PropertyChanged += (_, e) =>
        {
            if (e.Property != Visual.IsVisibleProperty) return;
            if (c.IsVisible) Enter(c, dx, dy);
            else
            {
                // hidden instantly: no transition left running on an invisible overlay
                c.SetValue(Generation, c.GetValue(Generation) + 1);
                c.Transitions = null;
                c.Opacity = 1;
                c.RenderTransform = null;
            }
        };
    }

    // ---------------------------------------------------------------- pressed feedback

    /// <summary>The scale a pressed control takes.</summary>
    public const double PressedScale = .94;

    private static readonly ITransform Pressed = TransformOperations.Parse("scale(0.94)");
    private static readonly ITransform Released = TransformOperations.Parse("scale(1)");

    /// <summary>
    /// The touch feedback (P1): while pressed the button shrinks a little (in <see cref="M.Press"/>) and springs back
    /// on release; the change is set in the same input event, so the next frame already shows it. Optionally a haptic
    /// tick (P7).
    /// </summary>
    public static T Pressable<T>(T button, bool haptic = false) where T : Button
    {
        button.RenderTransformOrigin = RelativePoint.Center;
        button.PropertyChanged += (_, e) =>
        {
            if (e.Property != Button.IsPressedProperty) return;
            bool down = button.IsPressed;
            if (M.Reduced) button.Transitions = null;
            else button.Transitions ??=
            [
                new TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = M.Press, Easing = M.EaseOut }
            ];
            button.RenderTransform = down ? Pressed : Released;
            if (down && haptic) PlatformHaptics.Tick();
        };
        return button;
    }

    /// <summary>True while the pressed feedback is showing (tests).</summary>
    public static bool ShowsPressed(Control c) => ReferenceEquals(c.RenderTransform, Pressed);
}
