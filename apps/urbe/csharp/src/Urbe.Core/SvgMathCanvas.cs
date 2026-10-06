using System.Drawing;
using System.Globalization;
using System.Text;
using CSharpMath.Rendering.FrontEnd;
using MathPath = CSharpMath.Rendering.FrontEnd.Path;

namespace Urbe.Core;

internal sealed class SvgMathCanvas : ICanvas
{
    private readonly StringBuilder _body = new();
    private readonly Stack<AffineTransform> _stack = new();
    private AffineTransform _transform = AffineTransform.Identity;

    public SvgMathCanvas(float width, float height)
    {
        Width = width;
        Height = height;
    }

    public float Width { get; }
    public float Height { get; }
    public Color DefaultColor { get; set; } = Color.Black;
    public Color? CurrentColor { get; set; }
    public PaintStyle CurrentStyle { get; set; } = PaintStyle.Fill;

    public MathPath StartNewPath() => new SvgMathPath(this);

    public void DrawLine(
        float x1,
        float y1,
        float x2,
        float y2,
        float lineThickness)
    {
        var a = Transform(x1, y1);
        var b = Transform(x2, y2);
        _body.Append("<line x1='").Append(N(a.X))
            .Append("' y1='").Append(N(a.Y))
            .Append("' x2='").Append(N(b.X))
            .Append("' y2='").Append(N(b.Y))
            .Append("' stroke='").Append(ColorCss(CurrentColor ?? DefaultColor))
            .Append("' stroke-width='").Append(N(Math.Max(.01f, lineThickness)))
            .Append("' fill='none'/>");
    }

    public void StrokeRect(float left, float top, float width, float height) =>
        EmitRect(left, top, width, height, fill: false);

    public void FillRect(float left, float top, float width, float height) =>
        EmitRect(left, top, width, height, fill: true);

    public void Save() => _stack.Push(_transform);

    public void Translate(float dx, float dy) =>
        _transform = _transform.Translate(dx, dy);

    public void Scale(float sx, float sy) =>
        _transform = _transform.Scale(sx, sy);

    public void Restore()
    {
        if (_stack.Count > 0)
            _transform = _stack.Pop();
    }

    internal PointF Transform(float x, float y) => _transform.Apply(x, y);

    internal void EmitPath(
        string data,
        Color? foreground,
        PaintStyle style)
    {
        if (data.Length == 0)
            return;

        var color = ColorCss(foreground ?? CurrentColor ?? DefaultColor);
        _body.Append("<path d='").Append(data).Append("'");
        if (style == PaintStyle.Fill)
        {
            _body.Append(" fill='").Append(color).Append("' stroke='none'");
        }
        else
        {
            _body.Append(" fill='none' stroke='").Append(color)
                .Append("' stroke-width='1'");
        }
        _body.Append("/>");
    }

    public string ToSvg(string? ariaLabel)
    {
        var width = N(Width);
        var height = N(Height);
        return "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 " +
               width + " " + height + "' width='" + width +
               "' height='" + height +
               "' role='img' aria-label='" +
               EscapeXml(ariaLabel ?? string.Empty) + "'>" +
               _body + "</svg>";
    }

    private void EmitRect(
        float left,
        float top,
        float width,
        float height,
        bool fill)
    {
        var a = Transform(left, top);
        var b = Transform(left + width, top);
        var c = Transform(left + width, top + height);
        var d = Transform(left, top + height);
        var path = "M" + N(a.X) + " " + N(a.Y) +
                   "L" + N(b.X) + " " + N(b.Y) +
                   "L" + N(c.X) + " " + N(c.Y) +
                   "L" + N(d.X) + " " + N(d.Y) + "Z";
        var color = ColorCss(CurrentColor ?? DefaultColor);
        _body.Append("<path d='").Append(path).Append("' ")
            .Append(fill ? "fill='" : "fill='none' stroke='")
            .Append(color)
            .Append(fill ? "' stroke='none'" : "' stroke-width='1'")
            .Append("/>");
    }

    internal static string N(float value)
    {
        if (Math.Abs(value) < .0005f)
            value = 0;
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string ColorCss(Color color)
    {
        var rgb = "#" + color.R.ToString("x2", CultureInfo.InvariantCulture) +
                  color.G.ToString("x2", CultureInfo.InvariantCulture) +
                  color.B.ToString("x2", CultureInfo.InvariantCulture);
        if (color.A == byte.MaxValue)
            return rgb;
        return "rgba(" + color.R + "," + color.G + "," + color.B + "," +
               (color.A / 255d).ToString("0.###", CultureInfo.InvariantCulture) + ")";
    }

    private static string EscapeXml(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private readonly record struct AffineTransform(
        float M11,
        float M12,
        float M21,
        float M22,
        float Dx,
        float Dy)
    {
        public static AffineTransform Identity { get; } =
            new(1, 0, 0, 1, 0, 0);

        public PointF Apply(float x, float y) =>
            new(
                M11 * x + M21 * y + Dx,
                M12 * x + M22 * y + Dy);

        public AffineTransform Translate(float dx, float dy) =>
            new(
                M11,
                M12,
                M21,
                M22,
                M11 * dx + M21 * dy + Dx,
                M12 * dx + M22 * dy + Dy);

        public AffineTransform Scale(float sx, float sy) =>
            new(
                M11 * sx,
                M12 * sx,
                M21 * sy,
                M22 * sy,
                Dx,
                Dy);
    }

    private sealed class SvgMathPath : MathPath
    {
        private readonly SvgMathCanvas _owner;
        private readonly StringBuilder _data = new();
        private bool _contourOpen;
        private bool _disposed;

        public SvgMathPath(SvgMathCanvas owner) => _owner = owner;

        public override Color? Foreground { get; set; }

        public override void MoveTo(float x0, float y0)
        {
            var point = _owner.Transform(x0, y0);
            _data.Append('M').Append(N(point.X)).Append(' ').Append(N(point.Y));
            _contourOpen = true;
        }

        public override void LineTo(float x1, float y1)
        {
            var point = _owner.Transform(x1, y1);
            _data.Append('L').Append(N(point.X)).Append(' ').Append(N(point.Y));
        }

        public override void Curve3(float x1, float y1, float x2, float y2)
        {
            var a = _owner.Transform(x1, y1);
            var b = _owner.Transform(x2, y2);
            _data.Append('Q').Append(N(a.X)).Append(' ').Append(N(a.Y))
                .Append(' ').Append(N(b.X)).Append(' ').Append(N(b.Y));
        }

        public override void Curve4(
            float x1,
            float y1,
            float x2,
            float y2,
            float x3,
            float y3)
        {
            var a = _owner.Transform(x1, y1);
            var b = _owner.Transform(x2, y2);
            var c = _owner.Transform(x3, y3);
            _data.Append('C').Append(N(a.X)).Append(' ').Append(N(a.Y))
                .Append(' ').Append(N(b.X)).Append(' ').Append(N(b.Y))
                .Append(' ').Append(N(c.X)).Append(' ').Append(N(c.Y));
        }

        public override void CloseContour()
        {
            if (!_contourOpen)
                return;
            _data.Append('Z');
            _contourOpen = false;
        }

        public override void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _owner.EmitPath(_data.ToString(), Foreground, _owner.CurrentStyle);
        }
    }
}
