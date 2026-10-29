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
/// Parameters (<c>a = 2 em 0..5</c>) are numbers the editor shows as sliders, read by the curves as they move (Desmos
/// does the same); <c>(cos(t), sin(t))</c> is a parametric curve over <c>t: 0..2pi</c>, for movements and other physics.
/// </summary>
public sealed class FunctionPlot
{
    /// <param name="ParametricX">x(t) for a parametric curve, whose <paramref name="Expression"/> is y(t).</param>
    public sealed record Function(string Source, Expression Expression, string Label, Expression? ParametricX = null);

    /// <summary>A number of the graph the person moves (a slider); the curves read <see cref="Value"/> as they are drawn.</summary>
    public sealed class Parameter(string name, double value, double min, double max)
    {
        public string Name { get; } = name;
        public double Value { get; set; } = value;
        public double Min { get; } = min;
        public double Max { get; } = max;
        /// <summary>The slider's step: a 200th of the range.</summary>
        public double Step => (Max - Min) / 200;
    }

    public List<Parameter> Parameters { get; } = [];
    public double TMin { get; private set; }
    public double TMax { get; private set; } = 2 * Math.PI;
    private bool _xGiven;

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

    private static readonly System.Text.RegularExpressions.Regex ParameterLine =
        new(@"^(?<lead>\s*)(?<name>\p{L}+)(?<eq>\s*=\s*)(?<value>.+?)(?<range>\s+(?:em|in)\s+.+?)?(?<trail>\s*)$");

    private static bool IsParameterLine(string line, out System.Text.RegularExpressions.Match match)
    {
        match = ParameterLine.Match(line);
        return match.Success && match.Groups["name"].Value is not ("x" or "X" or "y" or "Y" or "t");
    }

    public static FunctionPlot Parse(string text)
    {
        var plot = new FunctionPlot();
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var parameters = new Dictionary<string, Parameter>(StringComparer.Ordinal);
        // the parameters first, so a curve may use one written below it
        for (int i = 0; i < lines.Length; i++)
        {
            if (!IsParameterLine(lines[i], out var m)) continue;
            try
            {
                double value = Expression.Parse(m.Groups["value"].Value, constant: true).Evaluate(0);
                if (!double.IsFinite(value)) throw new FormatException("o parâmetro precisa de um número");
                double min, max;
                if (m.Groups["range"].Success)
                {
                    (min, max) = Range(System.Text.RegularExpressions.Regex.Replace(m.Groups["range"].Value, @"^\s+(?:em|in)\s+", ""));
                    if (value < min || value > max) throw new FormatException("o valor está fora do intervalo " + Format2(min) + ".." + Format2(max));
                }
                else (min, max) = Math.Abs(value) <= 10 ? (-10, 10) : (-2 * Math.Abs(value), 2 * Math.Abs(value));
                var name = m.Groups["name"].Value;
                if (parameters.ContainsKey(name)) throw new FormatException(name + " já foi definido");
                var parameter = new Parameter(name, value, min, max);
                parameters[name] = parameter;
                plot.Parameters.Add(parameter);
            }
            catch (FormatException error) { plot.Errors.Add("Linha " + (i + 1) + ": " + error.Message); }
        }
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("//", StringComparison.Ordinal) || IsParameterLine(lines[i], out _)) continue;
            try
            {
                var colon = line.IndexOf(':');
                var key = colon > 0 ? line[..colon].Trim().ToLowerInvariant() : "";
                if (key is "título" or "titulo" or "title") { plot.Title = line[(colon + 1)..].Trim(); continue; }
                if (key is "x" or "y" or "t")
                {
                    var (a, b) = Range(line[(colon + 1)..]);
                    if (key == "x") { plot.XMin = a; plot.XMax = b; plot._xGiven = true; }
                    else if (key == "y") { plot.YMin = a; plot.YMax = b; }
                    else { plot.TMin = a; plot.TMax = b; }
                    continue;
                }
                // (x(t), y(t)): a parametric curve
                if (line.StartsWith('(') && line.EndsWith(')') && TopLevelComma(line[1..^1]) is int comma)
                {
                    var inside = line[1..^1];
                    plot.Functions.Add(new Function(line, Expression.Parse(inside[(comma + 1)..], "t", parameters), line,
                        Expression.Parse(inside[..comma], "t", parameters)));
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
                plot.Functions.Add(new Function(source, Expression.Parse(source, "x", parameters), (eq >= 0 ? line[..eq].Trim() : "y") + " = " + source));
            }
            catch (FormatException error) { plot.Errors.Add("Linha " + (i + 1) + ": " + error.Message); }
        }
        if (plot.Functions.Count == 0 && plot.Errors.Count == 0) plot.Errors.Add("Escreva uma função, como y = sin(x).");
        return plot;
    }

    /// <summary>The comma between x(t) and y(t): outside parentheses, and not a decimal comma (1,5).</summary>
    private static int? TopLevelComma(string inside)
    {
        int depth = 0;
        var found = new List<int>();
        for (int k = 0; k < inside.Length; k++)
        {
            var c = inside[k];
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == ',' && depth == 0 && !(k > 0 && char.IsDigit(inside[k - 1]) && k + 1 < inside.Length && char.IsDigit(inside[k + 1]))) found.Add(k);
        }
        return found.Count == 1 ? found[0] : null;
    }

    /// <summary>
    /// The text with parameter <paramref name="name"/> set to <paramref name="value"/> (a slider let go): only its
    /// number changes, written with the note's decimal separator and spacing.
    /// </summary>
    public static string WithValue(string text, string name, double value)
    {
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (!IsParameterLine(line, out var m) || m.Groups["name"].Value != name) continue;
            var old = m.Groups["value"].Value;
            var number = Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);
            if (old.Contains(',') && !old.Contains('.')) number = number.Replace('.', ',');
            var at = m.Groups["value"].Index;
            lines[i] = line[..at] + number + line[(at + old.Length)..] + (lines[i].EndsWith('\r') ? "\r" : "");
            return string.Join('\n', lines);
        }
        return text;
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
        var raw = Functions.Select(f => f.ParametricX is { } fx
            ? Enumerable.Range(0, samples + 1).Select(k => TMin + (TMax - TMin) * k / samples)
                .Select(t => new PlotPoint(Safe(fx, t), Safe(f.Expression, t))).ToList()
            : Enumerable.Range(0, samples + 1).Select(k => XMin + (XMax - XMin) * k / samples)
                .Select(x => new PlotPoint(x, Safe(f.Expression, x))).ToList()).ToList();
        double xMin = XMin, xMax = XMax;
        // parametric curves only and no x: the view fits them, as it does with y
        if (!_xGiven && Functions.Count > 0 && Functions.All(f => f.ParametricX is not null))
            (xMin, xMax) = Fit(raw.SelectMany(c => c).Select(p => p.X), null, null);
        double yMin, yMax;
        (yMin, yMax) = Fit(raw.SelectMany(c => c).Select(p => p.Y), YMin, YMax);
        double span = yMax - yMin, middle = (yMin + yMax) / 2, xSpan = xMax - xMin, xMiddle = (xMin + xMax) / 2;
        var curves = new List<PlotCurve>();
        for (int c = 0; c < raw.Count; c++)
        {
            var segments = new List<IReadOnlyList<PlotPoint>>();
            var current = new List<PlotPoint>();
            PlotPoint? last = null;
            foreach (var p in raw[c])
            {
                bool ok = double.IsFinite(p.Y) && Math.Abs(p.Y) < 1e12 && double.IsFinite(p.X) && Math.Abs(p.X) < 1e12;
                // an asymptote: between two samples the curve leaps across the view from one side to the other
                bool jump = ok && last is not null &&
                    (Math.Abs(p.Y - last.Y) > span * 2 && Math.Sign(p.Y - middle) != Math.Sign(last.Y - middle) ||
                     Math.Abs(p.X - last.X) > xSpan * 2 && Math.Sign(p.X - xMiddle) != Math.Sign(last.X - xMiddle));
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
        return new PlotDrawing(Title, xMin, xMax, yMin, yMax, curves, Errors);
    }

    /// <summary>A range fitted to the values, ignoring the extremes an asymptote throws up; a given end is kept.</summary>
    private static (double, double) Fit(IEnumerable<double> values, double? min, double? max)
    {
        if (min is { } lo && max is { } hi) return (lo, hi);
        var sorted = values.Where(double.IsFinite).OrderBy(v => v).ToList();
        if (sorted.Count == 0) return (min ?? -1, max ?? 1);
        double a = sorted[(int)(sorted.Count * .02)], b = sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * .98))];
        if (b - a < 1e-9) { a -= 1; b += 1; }
        var pad = (b - a) * .08;
        return (min ?? a - pad, max ?? b + pad);
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

        /// <param name="variable">The variable the expression is of: x for a function, t for a parametric curve.</param>
        /// <param name="parameters">The graph's parameters, read as they are when evaluated (the sliders).</param>
        /// <param name="constant">A parameter's value: numbers and constants only.</param>
        public static Expression Parse(string text, string variable = "x", IReadOnlyDictionary<string, Parameter>? parameters = null, bool constant = false)
        {
            var parser = new Parser(text, variable, parameters, constant);
            var run = parser.Sum();
            parser.End();
            return new Expression(run);
        }

        private sealed class Parser(string text, string variable, IReadOnlyDictionary<string, Parameter>? parameters, bool constant)
        {
            /// <summary>The variable, a parameter or a constant called <paramref name="name"/>, or null.</summary>
            private Func<double, double>? Known(string name)
            {
                if (name == variable || variable == "x" && name == "X")
                    return constant ? throw new FormatException("o parâmetro precisa de um número, sem " + variable) : v => v;
                if (parameters is not null && parameters.TryGetValue(name, out var parameter)) return _ => parameter.Value;
                if (Constants.TryGetValue(name, out var k)) return _ => k;
                return null;
            }

            /// <summary>Names written together multiply (ax, 2ab, vt), as in Desmos: the longest known name first.</summary>
            private Func<double, double>? Split(string name)
            {
                for (int length = name.Length - 1; length >= 1; length--)
                {
                    if (Known(name[..length]) is not { } head) continue;
                    var rest = name[length..];
                    if ((Known(rest) ?? Split(rest)) is not { } tail) continue;
                    return v => head(v) * tail(v);
                }
                return null;
            }

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
                        // xsin(x), a sin(x) written together: the names before the function multiply it
                        for (int length = 1; length < name.Length; length++)
                        {
                            if (!Functions.TryGetValue(name[length..], out var g) || (Known(name[..length]) ?? Split(name[..length])) is not { } factor) continue;
                            _at++;
                            var arg = Sum();
                            if (Peek() != ')') throw new FormatException("falta fechar o parêntese de " + name[length..]);
                            _at++;
                            return x => factor(x) * g(arg(x));
                        }
                        // a(x + 1): a parameter times the parenthesis, read by the implicit product
                        if ((Known(name) ?? Split(name)) is { } times) return times;
                        throw new FormatException("função desconhecida: " + name);
                    }
                    if (Known(name) is { } known) return known;
                    if (Split(name) is { } product) return product;
                    throw new FormatException("\"" + name + "\" não é " + variable + ", um número, " + (parameters is { Count: > 0 } ? "um parâmetro, " : "") + "uma constante (pi, e) nem uma função");
                }
                throw new FormatException("não entendi \"" + c + "\"");
            }
        }
    }
}
