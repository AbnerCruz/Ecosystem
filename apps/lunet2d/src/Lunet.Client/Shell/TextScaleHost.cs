using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lunet.Client;

/// <summary>
/// UX12: the system font size (Android "Tamanho da fonte") enlarges the whole shell proportionally, so text, icons and
/// touch targets grow together and nothing is cut. The zoom stops where the logical width would drop below
/// <see cref="MinLogicalWidth"/> (the narrowest layout the shell is tested at); beyond that the text would no longer fit
/// a line of code or a button label, so that is the most it can grow on this screen.
/// </summary>
public sealed class TextScaleHost : LayoutTransformControl
{
    public const double MinLogicalWidth = 320;
    private double _requested = 1;

    public TextScaleHost(Control content)
    {
        Child = content;
        SizeChanged += (_, _) => Apply();
    }

    /// <summary>What the system asks (1 = normal, 1.3 = large…).</summary>
    public double Requested
    {
        get => _requested;
        set { _requested = double.IsFinite(value) && value > 0 ? value : 1; Apply(); }
    }

    /// <summary>The zoom in use: the requested one, limited by the screen width.</summary>
    public double Effective { get; private set; } = 1;

    public static double Limit(double requested, double width) =>
        Math.Max(1, Math.Min(requested, width > 0 ? width / MinLogicalWidth : requested));

    private void Apply()
    {
        var scale = Limit(_requested, Bounds.Width);
        if (Math.Abs(scale - Effective) < 0.001 && LayoutTransform is not null) return;
        Effective = scale;
        LayoutTransform = Math.Abs(scale - 1) < 0.001 ? null : new ScaleTransform(scale, scale);
    }
}
