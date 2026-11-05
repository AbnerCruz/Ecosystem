using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Urbe.Client;

/// <summary>
/// Typing in a long note without the touch selection handles relaying the whole text at every key (CST-114/129).
/// <para>
/// Avalonia 12.1.3's TextSelectionHandleCanvas (the touch handles of a TextBox) listens to the TextBox's
/// SelectionStart, SelectionEnd and CaretIndex and repositions its handles at each change, even when the handles are
/// hidden. A typed key changes all three (TextBox.HandleTextInput clears the selection and moves the caret), and each
/// selection change invalidates the presenter's TextLayout, so the hidden handles rebuilt the layout of the whole text
/// three to four times per key: 70% of a key in a 14 KB note in Fonte (bench ~115 ms/key, a bare TextBox ~58).
/// </para>
/// <para>
/// Here the canvas hears selection and caret changes only while it is visible (handles on screen). Hidden handles
/// have nothing to show, and the canvas places them again itself when it shows them (Show → MoveHandlesToSelection,
/// and its MeasureOverride while shown), so what the person sees is the same. Every other change (the text: hide the
/// handles, close the menu) still reaches it. If this Avalonia no longer has the members looked up here, nothing is
/// changed and the TextBox behaves as shipped; <c>QuietSelectionHandlesTests</c> fails then, so an upgrade is noticed.
/// </para>
/// </summary>
public static class QuietSelectionHandles
{
    private const string CanvasType = "Avalonia.Controls.Primitives.TextSelectionHandleCanvas";

    private static readonly PropertyInfo? CanvasOfPresenter =
        typeof(TextPresenter).GetProperty("TextSelectionHandleCanvas", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    private static readonly MethodInfo? Listener =
        typeof(TextBox).Assembly.GetType(CanvasType)?.GetMethod("TextBox_PropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly ConditionalWeakTable<TextBox, object> Quieted = new();

    /// <summary>Whether this Avalonia has the canvas and its listener where this class expects them.</summary>
    public static bool Supported => CanvasOfPresenter is not null && Listener is not null;

    /// <summary>
    /// Keeps <paramref name="box"/>'s handles quiet while hidden. The canvas is made when the box gets the focus (and
    /// again after the box leaves and re-enters the visual tree), so this looks again at each focus.
    /// </summary>
    [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicMethods, CanvasType, "Avalonia.Controls")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicProperties | DynamicallyAccessedMemberTypes.PublicProperties, typeof(TextPresenter))]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Members kept by the DynamicDependency attributes above.")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Members kept by the DynamicDependency attributes above.")]
    public static void Apply(TextBox box)
    {
        if (!Supported || Quieted.TryGetValue(box, out _)) return;
        Quieted.Add(box, new object());
        box.GotFocus += (_, _) => { Quiet(box); Dispatcher.UIThread.Post(() => Quiet(box), DispatcherPriority.Loaded); };
        if (box.IsFocused) Quiet(box);
    }

    /// <summary>The canvas of <paramref name="box"/> now, or null (no focus yet, or another Avalonia).</summary>
    public static Visual? CanvasOf(TextBox box)
    {
        if (CanvasOfPresenter is null) return null;
        var presenter = box.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        return presenter is null ? null : CanvasOfPresenter.GetValue(presenter) as Visual;
    }

    private static readonly ConditionalWeakTable<Visual, object> Done = new();

    private static void Quiet(TextBox box)
    {
        if (Listener is null || CanvasOfPresenter is null) return;
        var presenter = box.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        if (presenter is null || CanvasOfPresenter.GetValue(presenter) is not Visual canvas || Done.TryGetValue(canvas, out _)) return;
        EventHandler<AvaloniaPropertyChangedEventArgs> original;
        try { original = (EventHandler<AvaloniaPropertyChangedEventArgs>)Listener.CreateDelegate(typeof(EventHandler<AvaloniaPropertyChangedEventArgs>), canvas); }
        catch (ArgumentException) { return; }
        Done.Add(canvas, new object());
        box.PropertyChanged -= original;
        EventHandler<AvaloniaPropertyChangedEventArgs>? relay = null;
        relay = (sender, e) =>
        {
            if (!canvas.IsVisible && (e.Property == TextBox.SelectionStartProperty || e.Property == TextBox.SelectionEndProperty || e.Property == TextBox.CaretIndexProperty))
                return;
            // a canvas the box let go of (it left the tree) unsubscribed itself; its relay follows
            if (!ReferenceEquals(CanvasOfPresenter.GetValue(presenter), canvas)) { box.PropertyChanged -= relay; return; }
            original(sender, e);
        };
        box.PropertyChanged += relay;
    }
}
