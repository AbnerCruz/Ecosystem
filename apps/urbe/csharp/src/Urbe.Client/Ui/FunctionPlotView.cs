using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client.Ui;

/// <summary>
/// CST-069: a ```grafico block drawn in the note — grid, axes with round values, each function in its colour and a
/// legend; a line that could not be read says why under the graph. It is a picture of the block's text: a tap goes
/// on to the block, which opens its text for editing like any other.
/// </summary>
public sealed class FunctionPlotView : Control
{
    private readonly PlotDrawing _drawing;
    private readonly double _size;
    private const double Left = 38, Right = 10, Top = 8;

    public FunctionPlot Plot { get; }

    /// <summary>What it draws (tests): the curves and their ranges.</summary>
    public PlotDrawing Drawing => _drawing;

    public FunctionPlotView(FunctionPlot plot, double textSize)
    {
        Plot = plot;
        _size = textSize;
        _drawing = plot.Sample(480);
        Height = 240 + Bottom;
        Avalonia.Automation.AutomationProperties.SetName(this, "Gráfico: " + string.Join("; ", plot.Functions.Select(f => f.Label)));
    }

    private double TitleHeight => _drawing.Title is null ? 0 : _size * 1.4;
    private double Bottom => 22 + (_drawing.Curves.Count + _drawing.Errors.Count) * 18;

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        double top = Top + TitleHeight, w = Math.Max(10, bounds.Width - Left - Right), h = Math.Max(10, bounds.Height - top - Bottom);
        var d = _drawing;
        double X(double x) => Left + (x - d.XMin) / (d.XMax - d.XMin) * w;
        double Y(double y) => top + (d.YMax - y) / (d.YMax - d.YMin) * h;
        var text = UrbeTheme.Brush(UrbeTheme.Text);
        var muted = UrbeTheme.Brush(UrbeTheme.Text3);
        var grid = new Pen(UrbeTheme.Brush(UrbeTheme.Text, .08), 1);
        var axis = new Pen(UrbeTheme.Brush(UrbeTheme.Text, .45), 1.2);
        var face = new Typeface(UrbeTheme.UiFont);
        double labelSize = Math.Max(10, _size * .62);
        FormattedText Label(string s, IBrush brush, double size) => new(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, size, brush);

        if (d.Title is { } title)
        {
            var t = Label(title, text, _size * .85);
            context.DrawText(t, new Point(Left + (w - t.Width) / 2, Top));
        }
        context.FillRectangle(UrbeTheme.Brush(UrbeTheme.Text, .025), new Rect(Left, top, w, h), 6);
        foreach (var tick in FunctionPlot.Ticks(d.XMin, d.XMax))
        {
            context.DrawLine(grid, new Point(X(tick), top), new Point(X(tick), top + h));
            var t = Label(FunctionPlot.Format(tick), muted, labelSize);
            context.DrawText(t, new Point(X(tick) - t.Width / 2, top + h + 3));
        }
        foreach (var tick in FunctionPlot.Ticks(d.YMin, d.YMax))
        {
            context.DrawLine(grid, new Point(Left, Y(tick)), new Point(Left + w, Y(tick)));
            var t = Label(FunctionPlot.Format(tick), muted, labelSize);
            context.DrawText(t, new Point(Left - 4 - t.Width, Y(tick) - t.Height / 2));
        }
        if (d.YMin <= 0 && d.YMax >= 0) context.DrawLine(axis, new Point(Left, Y(0)), new Point(Left + w, Y(0)));
        if (d.XMin <= 0 && d.XMax >= 0) context.DrawLine(axis, new Point(X(0), top), new Point(X(0), top + h));

        using (context.PushClip(new Rect(Left, top, w, h)))
        {
            foreach (var curve in d.Curves)
            {
                var pen = new Pen(UrbeTheme.Brush(FunctionPlot.Colors[curve.Index % FunctionPlot.Colors.Length]), 2.2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                foreach (var segment in curve.Segments)
                {
                    var geometry = new StreamGeometry();
                    using (var g = geometry.Open())
                    {
                        double span = d.YMax - d.YMin;
                        Point P(PlotPoint p) => new(X(p.X), Y(Math.Clamp(p.Y, d.YMin - span, d.YMax + span)));
                        g.BeginFigure(P(segment[0]), false);
                        for (int i = 1; i < segment.Count; i++) g.LineTo(P(segment[i]));
                        g.EndFigure(false);
                    }
                    context.DrawGeometry(null, pen, geometry);
                }
            }
        }
        double y = top + h + 22;
        foreach (var curve in d.Curves)
        {
            var color = UrbeTheme.Brush(FunctionPlot.Colors[curve.Index % FunctionPlot.Colors.Length]);
            context.FillRectangle(color, new Rect(Left, y + 6, 14, 3), 1.5f);
            context.DrawText(Label(curve.Label, text, labelSize * 1.05), new Point(Left + 20, y));
            y += 18;
        }
        foreach (var error in d.Errors)
        {
            context.DrawText(Label(error, UrbeTheme.Brush(UrbeTheme.Danger), labelSize), new Point(Left, y));
            y += 18;
        }
    }
}
