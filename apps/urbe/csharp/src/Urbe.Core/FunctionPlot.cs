using System.Globalization;
using System.Text;

namespace Urbe.Core;

public sealed record PlotPoint(double X, double Y);
public sealed record PlotCurve(int Index, string Label, IReadOnlyList<IReadOnlyList<PlotPoint>> Segments);
public sealed record PlotDrawing(string? Title, double XMin, double XMax, double YMin, double YMax, IReadOnlyList<PlotCurve> Curves, IReadOnlyList<string> Errors);

/// <summary>
/// CST-069: a ```grafico block of a note — the functions it lists, drawn over x (Desmos and Obsidian's function-plot
/// keep a graph as text in the same way; the note stays plain Markdown). One line per thing:
/// <c>y = sin(x)</c> or <c>f(x) = x^2/4</c> (a function), <c>x: -6..6</c> and <c>y: -2..4</c> (the ranges; without y the
/// curves are fitted), <c>título: Ondas</c>. Expressions are read by a small closed evaluator — numbers, x, + - * / ^,
/// parentheses, implicit products (2x, 3(x+1)) and a fixed set of functions and constants: nothing is ever run.
/// </summary>
public sealed class FunctionPlot
{
    public sealed record Function(string Source, Expression Expression, string Label);

    public string? Title { get; private set; }
    public List<Function> Functions { get; } = [];
    public double XMin { get; private set; } = -10;
    public double XMax { get; private set; } = 10;
    public double? YMin { get; private set; }
    public double? YMax { get; private set; }
    public List<string> Errors { get; } = [];

    public const string Language = "grafico";

    /// <summary>The code-block languages that are graphs.</summary>
    public static bool IsPlotLanguage(string? lang) => lang is not null && lang.Trim().ToLowerInvariant() is "grafico" or "gráfico" or "plot";

    public static FunctionPlot Parse(string text)
    {
        var plot = new FunctionPlot();
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("//", StringComparison.Ordinal)) continue;
            try
            {
                var colon = line.IndexOf(':');
                var key = colon > 0 ? line[..colon].Trim().ToLowerInvariant() : "";
                if (key is "título" or "titulo" or "title") { plot.Title = line[(colon + 1)..].Trim(); continue; }
                if (key is "x" or "y")
                {
                    var (a, b) = Range(line[(colon + 1)..]);
                    if (key == "x") { plot.XMin = a; plot.XMax = b; }
                    else { plot.YMin = a; plot.YMax = b; }
                    continue;
                }
                var source = line;
                var eq = line.IndexOf('=');
                if (eq >= 0)
                {
                    var left = line[..eq].Trim().Replace(" ", "");
                    if (left != "y" && !System.Text.RegularExpressions.Regex.IsMatch(left, @"^\p{L}\w*\(x\)$"))
                        throw new FormatException("antes do = use y ou f(x)");
                    source = line[(eq + 1)..].Trim();
                }
                plot.Functions.Add(new Function(source, Expression.Parse(source), (eq >= 0 ? line[..eq].Trim() : "y") + " = " + source));
            }
            catch (FormatException error) { plot.Errors.Add("Linha " + (i + 1) + ": " + error.Message); }
        }
        if (plot.Functions.Count == 0 && plot.Errors.Count == 0) plot.Errors.Add("Escreva uma função, como y = sin(x).");
        return plot;
    }

    private static (double, double) Range(string text)
    {
        var parts = text.Split("..", StringSplitOptions.TrimEntries);
        if (parts.Length != 2) throw new FormatException("escreva o intervalo como -5..5");
        double a = Expression.Parse(parts[0]).Evaluate(0), b = Expression.Parse(parts[1]).Evaluate(0);
        if (!double.IsFinite(a) || !double.IsFinite(b) || a >= b) throw new FormatException("o intervalo precisa ir de um número menor a um maior");
        return (a, b);
    }

    /// <summary>The curves sampled at <paramref name="samples"/> steps, cut where they leave the plane or jump (asymptotes).</summary>
    public PlotDrawing Sample(int samples = 400)
    {
        samples = Math.Clamp(samples, 16, 4000);
        var raw = Functions.Select(f => Enumerable.Range(0, samples + 1)
            .Select(k => XMin + (XMax - XMin) * k / samples)
            .Select(x => new PlotPoint(x, Safe(f.Expression, x))).ToList()).ToList();
        double yMin, yMax;
        if (YMin is { } lo && YMax is { } hi) (yMin, yMax) = (lo, hi);
        else
        {
            // fitted to the curves, ignoring the extremes an asymptote throws up
            var ys = raw.SelectMany(c => c).Select(p => p.Y).Where(double.IsFinite).OrderBy(y => y).ToList();
            if (ys.Count == 0) (yMin, yMax) = (-1, 1);
            else
            {
                double a = ys[(int)(ys.Count * .02)], b = ys[Math.Min(ys.Count - 1, (int)(ys.Count * .98))];
                if (b - a < 1e-9) { a -= 1; b += 1; }
                var pad = (b - a) * .08;
                (yMin, yMax) = (YMin ?? a - pad, YMax ?? b + pad);
            }
        }
        double span = yMax - yMin, middle = (yMin + yMax) / 2;
        var curves = new List<PlotCurve>();
        for (int c = 0; c < raw.Count; c++)
        {
            var segments = new List<IReadOnlyList<PlotPoint>>();
            var current = new List<PlotPoint>();
            PlotPoint? last = null;
            foreach (var p in raw[c])
            {
                bool ok = double.IsFinite(p.Y) && Math.Abs(p.Y) < 1e12;
                // an asymptote: between two samples the curve leaps across the view from one side to the other
                bool jump = ok && last is not null && Math.Abs(p.Y - last.Y) > span * 2 && Math.Sign(p.Y - middle) != Math.Sign(last.Y - middle);
                if (!ok || jump)
                {
                    if (current.Count > 1) segments.Add(current);
                    current = [];
                    last = null;
                    if (!ok) continue;
                }
                current.Add(p);
                last = p;
            }
            if (current.Count > 1) segments.Add(current);
            curves.Add(new PlotCurve(c, Functions[c].Label, segments));
        }
        return new PlotDrawing(Title, XMin, XMax, yMin, yMax, curves, Errors);
    }

    private static double Safe(Expression e, double x)
    {
        try { return e.Evaluate(x); } catch (ArithmeticException) { return double.NaN; }
    }

    /// <summary>Round values (1, 2 or 5 × 10ⁿ apart) for about <paramref name="target"/> marks between the two ends.</summary>
    public static IReadOnlyList<double> Ticks(double min, double max, int target = 6)
    {
        var raw = (max - min) / Math.Max(1, target - 1);
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var step = new[] { 1, 2, 2.5, 5, 10 }.Select(m => m * magnitude).First(s => s >= raw - 1e-12);
        var ticks = new List<double>();
        for (double v = Math.Ceiling(min / step - 1e-9) * step; v <= max + step * 1e-9; v += step)
            ticks.Add(Math.Round(v, 10) == 0 ? 0 : Math.Round(v, 10));
        return ticks;
    }

    public static string Format(double v) => v.ToString("0.###", CultureInfo.GetCultureInfo("pt-BR"));

    /// <summary>The colours of the curves, in order (they read on light and dark pages).</summary>
    public static readonly string[] Colors = ["#4f8cff", "#e5484d", "#30a46c", "#f5a524", "#8e4ec6", "#12a594"];

    /// <summary>The graph as an SVG for pages and exports: axes, grid, the curves and their legend.</summary>
    public string ToSvg(double width = 560, double height = 340)
    {
        var d = Sample();
        double left = 40, right = 12, top = d.Title is null ? 12 : 30, bottom = 28 + 16 * Math.Max(1, d.Curves.Count);
        double w = width - left - right, h = height - top - bottom;
        string X(double x) => Format2(left + (x - d.XMin) / (d.XMax - d.XMin) * w);
        string Y(double y) => Format2(top + (d.YMax - y) / (d.YMax - d.YMin) * h);
        var svg = new StringBuilder();
        svg.Append("<svg xmlns='http://www.w3.org/2000/svg' class='urbe-plot' viewBox='0 0 ").Append(Format2(width)).Append(' ').Append(Format2(height))
           .Append("' width='100%' role='img' font-family='sans-serif' font-size='11'>");
        if (d.Title is { } title) svg.Append("<text x='").Append(Format2(width / 2)).Append("' y='18' text-anchor='middle' font-size='13' font-weight='600' fill='currentColor'>").Append(Esc(title)).Append("</text>");
        svg.Append("<g stroke='currentColor' stroke-opacity='.12'>");
        foreach (var t in Ticks(d.XMin, d.XMax)) svg.Append("<line x1='").Append(X(t)).Append("' x2='").Append(X(t)).Append("' y1='").Append(Format2(top)).Append("' y2='").Append(Format2(top + h)).Append("'/>");
        foreach (var t in Ticks(d.YMin, d.YMax)) svg.Append("<line x1='").Append(Format2(left)).Append("' x2='").Append(Format2(left + w)).Append("' y1='").Append(Y(t)).Append("' y2='").Append(Y(t)).Append("'/>");
        svg.Append("</g><g stroke='currentColor' stroke-opacity='.55'>");
        if (d.YMin <= 0 && d.YMax >= 0) svg.Append("<line x1='").Append(Format2(left)).Append("' x2='").Append(Format2(left + w)).Append("' y1='").Append(Y(0)).Append("' y2='").Append(Y(0)).Append("'/>");
        if (d.XMin <= 0 && d.XMax >= 0) svg.Append("<line x1='").Append(X(0)).Append("' x2='").Append(X(0)).Append("' y1='").Append(Format2(top)).Append("' y2='").Append(Format2(top + h)).Append("'/>");
        svg.Append("</g><g fill='currentColor' fill-opacity='.6'>");
        foreach (var t in Ticks(d.XMin, d.XMax)) svg.Append("<text x='").Append(X(t)).Append("' y='").Append(Format2(top + h + 13)).Append("' text-anchor='middle'>").Append(Format(t)).Append("</text>");
        foreach (var t in Ticks(d.YMin, d.YMax)) svg.Append("<text x='").Append(Format2(left - 4)).Append("' y='").Append(Y(t)).Append("' dy='4' text-anchor='end'>").Append(Format(t)).Append("</text>");
        svg.Append("</g><svg x='").Append(Format2(left)).Append("' y='").Append(Format2(top)).Append("' width='").Append(Format2(w)).Append("' height='").Append(Format2(h))
           .Append("' viewBox='").Append(Format2(left)).Append(' ').Append(Format2(top)).Append(' ').Append(Format2(w)).Append(' ').Append(Format2(h)).Append("' overflow='hidden'>");
        foreach (var c in d.Curves)
            foreach (var s in c.Segments)
                svg.Append("<path fill='none' stroke-width='2' stroke-linejoin='round' stroke='").Append(Colors[c.Index % Colors.Length]).Append("' d='M")
                   .Append(string.Join(" L", s.Select(p => X(p.X) + " " + Y(Math.Clamp(p.Y, d.YMin - (d.YMax - d.YMin), d.YMax + (d.YMax - d.YMin)))))).Append("'/>");
        svg.Append("</svg>");
        for (int i = 0; i < d.Curves.Count; i++)
            svg.Append("<text x='").Append(Format2(left)).Append("' y='").Append(Format2(top + h + 30 + 16 * i)).Append("' fill='").Append(Colors[i % Colors.Length]).Append("'>").Append(Esc(d.Curves[i].Label)).Append("</text>");
        return svg.Append("</svg>").ToString();
    }

    private static string Format2(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("'", "&#39;");

    /// <summary>An expression of x, read once and evaluated many times.</summary>
    public sealed class Expression
    {
        private readonly Func<double, double> _run;
        private Expression(Func<double, double> run) => _run = run;
        public double Evaluate(double x) => _run(x);

        private static readonly Dictionary<string, Func<double, double>> Functions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["sin"] = Math.Sin, ["sen"] = Math.Sin, ["cos"] = Math.Cos, ["tan"] = Math.Tan, ["tg"] = Math.Tan,
            ["asin"] = Math.Asin, ["arcsen"] = Math.Asin, ["arcsin"] = Math.Asin, ["acos"] = Math.Acos, ["arccos"] = Math.Acos,
            ["atan"] = Math.Atan, ["arctan"] = Math.Atan, ["arctg"] = Math.Atan, ["sinh"] = Math.Sinh, ["cosh"] = Math.Cosh, ["tanh"] = Math.Tanh,
            ["sqrt"] = Math.Sqrt, ["raiz"] = Math.Sqrt, ["abs"] = Math.Abs, ["ln"] = Math.Log, ["log"] = Math.Log10, ["exp"] = Math.Exp,
            ["floor"] = Math.Floor, ["piso"] = Math.Floor, ["ceil"] = Math.Ceiling, ["teto"] = Math.Ceiling, ["round"] = Math.Round, ["sign"] = v => Math.Sign(v), ["sinal"] = v => Math.Sign(v),
        };

        private static readonly Dictionary<string, double> Constants = new(StringComparer.OrdinalIgnoreCase) { ["pi"] = Math.PI, ["π"] = Math.PI, ["e"] = Math.E, ["tau"] = Math.Tau };

        public static Expression Parse(string text)
        {
            var parser = new Parser(text);
            var run = parser.Sum();
            parser.End();
            return new Expression(run);
        }

        private sealed class Parser(string text)
        {
            private int _at;

            private char Peek() { Skip(); return _at < text.Length ? text[_at] : '\0'; }
            private void Skip() { while (_at < text.Length && char.IsWhiteSpace(text[_at])) _at++; }

            public void End()
            {
                if (Peek() != '\0') throw new FormatException("não entendi \"" + text[_at..] + "\"");
            }

            public Func<double, double> Sum()
            {
                var left = Product();
                while (true)
                {
                    var c = Peek();
                    if (c is not ('+' or '-')) return left;
                    _at++;
                    var right = Product();
                    var l = left;
                    left = c == '+' ? x => l(x) + right(x) : x => l(x) - right(x);
                }
            }

            private Func<double, double> Product()
            {
                var left = Unary();
                while (true)
                {
                    var c = Peek();
                    if (c is '*' or '/' or '·' or '×' or '÷')
                    {
                        _at++;
                        if (Peek() is '*' or '/') throw new FormatException("\"" + c + text[_at] + "\" não é um operador; use ^ para potência");
                        var right = Unary();
                        var l = left;
                        left = c is '/' or '÷' ? x => l(x) / right(x) : x => l(x) * right(x);
                    }
                    else if (c == '(' || char.IsLetterOrDigit(c) || c == 'π')
                    {
                        // an implicit product: 2x, 3(x + 1), 2sin(x)
                        var right = Unary();
                        var l = left;
                        left = x => l(x) * right(x);
                    }
                    else return left;
                }
            }

            private Func<double, double> Unary()
            {
                var c = Peek();
                if (c == '-') { _at++; var inner = Unary(); return x => -inner(x); }
                if (c == '+') { _at++; return Unary(); }
                return Power();
            }

            private Func<double, double> Power()
            {
                var b = Primary();
                if (Peek() != '^') return b;
                _at++;
                var exponent = Unary();
                return x => Math.Pow(b(x), exponent(x));
            }

            private Func<double, double> Primary()
            {
                var c = Peek();
                if (c == '\0') throw new FormatException("expressão incompleta");
                if (c == '(')
                {
                    _at++;
                    var inner = Sum();
                    if (Peek() != ')') throw new FormatException("falta fechar um parêntese");
                    _at++;
                    return inner;
                }
                if (c == '|')
                {
                    _at++;
                    var inner = Sum();
                    if (Peek() != '|') throw new FormatException("falta fechar o módulo |…|");
                    _at++;
                    return x => Math.Abs(inner(x));
                }
                if (char.IsDigit(c) || c == '.' || c == ',')
                {
                    int start = _at;
                    while (_at < text.Length && (char.IsDigit(text[_at]) || (text[_at] is '.' or ',' && _at + 1 < text.Length && char.IsDigit(text[_at + 1])))) _at++;
                    if (!double.TryParse(text[start.._at].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                        throw new FormatException("número inválido: " + text[start.._at]);
                    return _ => value;
                }
                if (char.IsLetter(c) || c == 'π')
                {
                    if (c == 'π') { _at++; return _ => Math.PI; }
                    int start = _at;
                    while (_at < text.Length && char.IsLetter(text[_at])) _at++;
                    var name = text[start.._at];
                    // a name read greedily may be a function followed by x ("sinx" is not allowed; "xsin(x)" is x·sin(x))
                    if (Peek() == '(')
                    {
                        if (Functions.TryGetValue(name, out var f))
                        {
                            _at++;
                            var arg = Sum();
                            if (Peek() != ')') throw new FormatException("falta fechar o parêntese de " + name);
                            _at++;
                            return x => f(arg(x));
                        }
                        if (name.Length > 1 && name.StartsWith('x') && Functions.TryGetValue(name[1..], out var g))
                        {
                            _at++;
                            var arg = Sum();
                            if (Peek() != ')') throw new FormatException("falta fechar o parêntese de " + name[1..]);
                            _at++;
                            return x => x * g(arg(x));
                        }
                        throw new FormatException("função desconhecida: " + name);
                    }
                    if (name == "x" || name == "X") return x => x;
                    if (Constants.TryGetValue(name, out var k)) return _ => k;
                    if (name.All(ch => ch is 'x' or 'X')) { int n = name.Length; return x => Math.Pow(x, n); }
                    throw new FormatException("\"" + name + "\" não é x, um número, uma constante (pi, e) nem uma função");
                }
                throw new FormatException("não entendi \"" + c + "\"");
            }
        }
    }
}
