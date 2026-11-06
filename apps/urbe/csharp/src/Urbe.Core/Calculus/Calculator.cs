using System.Text.RegularExpressions;

namespace Urbe.Core.Calculus;

/// <summary>What a line of calculation gave: its text, whether it is exact, and an error in words when it failed.</summary>
public sealed record CalcLine(string Input, string? Result, string? Error = null, bool Approximate = false, bool Definition = false)
{
    public bool Ok => Error is null;
}

/// <summary>
/// CST-206: the calculator of the Urbe — the same engine for a <c>```calculo</c> block of a note, for publications
/// and for the Assistente (exact arithmetic instead of a model's guess). One line at a time, in order, with the
/// variables and functions the lines before defined:
/// <list type="bullet">
/// <item><c>a = 3</c>, <c>f(x) = x^2 + 1</c> — definitions</item>
/// <item><c>2a + f(2)</c>, <c>media(3; 5; 8)</c>, <c>det([[1; 2]; [3; 4]])</c> — values</item>
/// <item><c>derivada de x^3 + 2x</c>, <c>simplificar (x+1)^2</c></item>
/// <item><c>resolver x^2 - 5x + 6 = 0</c>, <c>resolver 2^x = 10 para x</c></item>
/// <item><c>integral de 3x^2</c>, <c>integral de sen(x) de 0 até pi</c></item>
/// <item><c>100 km/h em m/s</c>, <c>37 °C em °F</c> — conversions with dimension check</item>
/// </list>
/// A line starting with <c>//</c> or <c>#</c> is a comment. Nothing here runs code.
/// </summary>
public static partial class Calculator
{
    /// <summary>The code-block languages that are calculations (<c>```calculo</c>).</summary>
    public static bool IsLanguage(string? lang) => lang is not null && lang.Trim().ToLowerInvariant() is "calculo" or "cálculo" or "calc" or "calculadora";

    /// <summary>
    /// Every <c>```calculo</c> block of rendered HTML as a table of its lines and their results (publications,
    /// exports): the note keeps only the lines; results are computed again on every render, never stored.
    /// </summary>
    public static string RenderHtml(string html) => CalcBlock().Replace(html, m =>
    {
        var sb = new System.Text.StringBuilder("<table class=\"urbe-calc\"><tbody>");
        foreach (var line in Run(System.Net.WebUtility.HtmlDecode(m.Groups[1].Value)))
        {
            if (line.Input.Trim().Length == 0) continue;
            var comment = line.Result is null && line.Error is null;
            sb.Append("<tr><td><code>").Append(MarkdownEngine.EscapeHtml(line.Input.Trim())).Append("</code></td><td")
              .Append(line.Ok ? "" : " class=\"urbe-calc-error\"").Append('>')
              .Append(MarkdownEngine.EscapeHtml(comment ? "" : line.Result ?? line.Error ?? "")).Append("</td></tr>");
        }
        return sb.Append("</tbody></table>").ToString();
    });

    [GeneratedRegex("<pre data-lang=\"(?:calculo|cálculo|calc|calculadora)\"><code>(.*?)</code></pre>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex CalcBlock();

    public sealed class Scope
    {
        public Dictionary<string, MathValue> Values { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, UserFunction> Functions { get; } = new(StringComparer.Ordinal);
        public HashSet<string> FunctionNames => [.. Functions.Keys];
    }

    /// <summary>Every line of a block, each with the definitions of the lines above it.</summary>
    public static IReadOnlyList<CalcLine> Run(string block, Scope? scope = null)
    {
        scope ??= new Scope();
        var results = new List<CalcLine>();
        foreach (var raw in block.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal) || line.StartsWith('#')) { results.Add(new CalcLine(raw, null)); continue; }
            try { results.Add(Line(line, scope) with { Input = raw }); }
            catch (MathError ex) { results.Add(new CalcLine(raw, null, ex.Message)); }
            catch (Exception ex) when (ex is OverflowException or ArithmeticException or StackOverflowException or InvalidCastException)
            { results.Add(new CalcLine(raw, null, "Não consegui calcular esta linha.")); }
        }
        return results;
    }

    /// <summary>One question (the Assistente's tool): the same rules as a line of a block.</summary>
    public static CalcLine Evaluate(string question, Scope? scope = null)
    {
        try { return Line(question.Trim(), scope ?? new Scope()); }
        catch (MathError ex) { return new CalcLine(question, null, ex.Message); }
    }

    private static CalcLine Line(string line, Scope scope)
    {
        var names = scope.FunctionNames;
        // f(x) = …, g(x; y) = …
        if (FunctionDefinition().Match(line) is { Success: true } fd && !MathFunctions.IsFunction(fd.Groups[1].Value))
        {
            var name = fd.Groups[1].Value;
            var parameters = fd.Groups[2].Value.Split([';', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            names.Add(name);
            var body = MathParser.Parse(fd.Groups[3].Value, names);
            scope.Functions[name] = new UserFunction(name, parameters, body);
            return new CalcLine(line, name + "(" + string.Join("; ", parameters) + ") = " + MathSymbolic.Simplify(body), Definition: true);
        }
        // a = …
        if (Assignment().Match(line) is { Success: true } asg)
        {
            var value = MathEvaluator.Evaluate(MathParser.Parse(asg.Groups[2].Value, names), scope.Values, scope.Functions);
            scope.Values[asg.Groups[1].Value] = value;
            return new CalcLine(line, asg.Groups[1].Value + " = " + Show(value), Definition: true);
        }
        if (Command(line, "derivada", "deriv", "d/dx", "derive", "derivative") is { } dExpr)
        {
            var (expr, x) = WithVariable(dExpr, names, scope);
            return new CalcLine(line, MathFormat.Write(MathSymbolic.Derivative(Inline(expr, scope), x)));
        }
        if (Command(line, "expandir", "expand") is { } xExpr)
            return new CalcLine(line, MathFormat.Write(MathSymbolic.Simplify(Inline(MathParser.Parse(xExpr, names), scope), expand: true)));
        if (Command(line, "simplificar", "simplify") is { } sExpr)
            return new CalcLine(line, MathFormat.Write(MathSymbolic.Simplify(Inline(MathParser.Parse(sExpr, names), scope))));
        if (Command(line, "resolver", "solve", "raizes de", "raízes de", "raizes", "raízes") is { } eq)
            return SolveLine(line, eq, scope);
        if (Command(line, "integral", "integrar", "integrate", "∫") is { } iExpr)
            return IntegralLine(line, iExpr, scope);
        if (Conversion().Match(line) is { Success: true } conv && MathUnits.Parse(conv.Groups[2].Value) is { } target)
        {
            var value = MathEvaluator.Evaluate(MathParser.Parse(conv.Groups[1].Value, names), scope.Values, scope.Functions);
            return new CalcLine(line, Convert(value, target));
        }
        var parsed = MathParser.Parse(line, names);
        if (parsed is Equation) return SolveLine(line, line, scope);
        var free = MathSymbolic.Variables(parsed).Where(v => !scope.Values.ContainsKey(v) && !MathUnits.TryGet(v, out _)).ToList();
        if (free.Count > 0)
            // an expression of names with no value: tidy it instead of failing (x^2 + 2x + x → x^2 + 3x)
            return new CalcLine(line, MathFormat.Write(MathSymbolic.Simplify(Inline(MathSymbolic.Substitute(parsed, scope.Values, ""), scope))));
        var result = MathEvaluator.Evaluate(parsed, scope.Values, scope.Functions);
        return new CalcLine(line, Show(result));
    }

    private static CalcLine SolveLine(string line, string text, Scope scope)
    {
        var (expr, x) = WithVariable(text, scope.FunctionNames, scope);
        var result = MathSymbolic.Solve(Inline(expr, scope), x, scope.Values);
        if (result.Roots.Count == 0) return new CalcLine(line, result.Note ?? "Sem solução.");
        var roots = string.Join("; ", result.Roots.Select(r => x + " = " + (r.Exact ? "" : "≈ ") + MathFormat.Number(r.Value)));
        if (result.ExactForm is { } exact) roots += "  (" + exact + ")";
        return new CalcLine(line, roots, Approximate: result.Roots.Any(r => !r.Exact) || result.ExactForm is not null);
    }

    private static CalcLine IntegralLine(string line, string text, Scope scope)
    {
        string? from = null, to = null;
        if (DefiniteBounds().Match(text) is { Success: true } bounds)
        {
            text = bounds.Groups[1].Value;
            from = bounds.Groups[2].Value;
            to = bounds.Groups[3].Value;
        }
        var (expr, x) = WithVariable(text, scope.FunctionNames, scope);
        expr = Inline(MathSymbolic.Substitute(expr, scope.Values, x), scope);
        var anti = MathSymbolic.Antiderivative(expr, x);
        if (from is null)
            return anti is null
                ? new CalcLine(line, null, "Não encontrei uma primitiva exata. Dê os limites (… de a até b) para o valor numérico.")
                : new CalcLine(line, MathFormat.Write(anti) + " + C");
        double a = Number(from, scope), b = Number(to!, scope);
        if (anti is not null)
        {
            try
            {
                var exact = MathEvaluator.Number(anti, x, b, scope.Values) - MathEvaluator.Number(anti, x, a, scope.Values);
                var check = MathSymbolic.Integrate(t => MathEvaluator.Number(expr, x, t, scope.Values), a, b);
                // the primitive is trusted only where the function is continuous: compare with the numeric value
                if (double.IsFinite(exact) && Math.Abs(exact - check.Value) <= 1e-6 * Math.Max(1, Math.Abs(exact)))
                    return new CalcLine(line, MathFormat.Number(exact));
            }
            catch (MathError) { }
        }
        var (value, error) = MathSymbolic.Integrate(t => MathEvaluator.Number(expr, x, t, scope.Values), a, b);
        return new CalcLine(line, "≈ " + MathFormat.Number(value) + "  (erro estimado < " + error.ToString("0.#E+0", MathFormat.PtBr) + ")", Approximate: true);
    }

    private static double Number(string text, Scope scope) =>
        MathEvaluator.Evaluate(MathParser.Parse(text, scope.FunctionNames), scope.Values, scope.Functions) is Scalar { Pure: true } s
            ? s.Value : throw new MathError("O limite “" + text + "” precisa ser um número.");

    /// <summary>The expression and the variable to work on: “… para t” names it, else the single free name, else x.</summary>
    private static (MathExpr Expr, string Variable) WithVariable(string text, IReadOnlySet<string> names, Scope scope)
    {
        string? variable = null;
        if (ForVariable().Match(text) is { Success: true } m) { text = m.Groups[1].Value; variable = m.Groups[2].Value; }
        var expr = MathParser.Parse(text, names);
        if (expr is Var v && scope.Functions.TryGetValue(v.Name, out var fn) && fn.Parameters.Count == 1)
            expr = new Call(fn.Name, [new Var(fn.Parameters[0])]);
        if (variable is null)
        {
            var free = MathSymbolic.Variables(Inline(expr, scope)).Where(n => !scope.Values.ContainsKey(n)).ToList();
            variable = free.Count == 1 ? free[0] : free.Contains("x") ? "x" : free.FirstOrDefault() ?? "x";
        }
        return (expr, variable);
    }

    /// <summary>The note's functions written out (f(2x) → (2x)^2 + 1), for derivatives and equations.</summary>
    private static MathExpr Inline(MathExpr e, Scope scope, int depth = 0)
    {
        if (depth > 32) throw new MathError("Funções que se chamam sem fim.");
        MathExpr Go(MathExpr x) => Inline(x, scope, depth + 1);
        return e switch
        {
            Call c when scope.Functions.TryGetValue(c.Name, out var fn) && c.Args.Count == fn.Parameters.Count =>
                Go(Replace(fn.Body, fn.Parameters.Zip(c.Args.Select(Go)).ToDictionary(p => p.First, p => p.Second))),
            Neg n => new Neg(Go(n.Arg)),
            Add a => new Add(Go(a.Left), Go(a.Right)),
            Sub s => new Sub(Go(s.Left), Go(s.Right)),
            Mul m => new Mul(Go(m.Left), Go(m.Right)),
            Div d => new Div(Go(d.Left), Go(d.Right)),
            Pow p => new Pow(Go(p.Base), Go(p.Exponent)),
            Call c2 => new Call(c2.Name, c2.Args.Select(Go).ToArray()),
            Equation q => new Equation(Go(q.Left), Go(q.Right)),
            _ => e
        };
    }

    private static MathExpr Replace(MathExpr e, IReadOnlyDictionary<string, MathExpr> map) => e switch
    {
        Var v when map.TryGetValue(v.Name, out var r) => r,
        Neg n => new Neg(Replace(n.Arg, map)),
        Add a => new Add(Replace(a.Left, map), Replace(a.Right, map)),
        Sub s => new Sub(Replace(s.Left, map), Replace(s.Right, map)),
        Mul m => new Mul(Replace(m.Left, map), Replace(m.Right, map)),
        Div d => new Div(Replace(d.Left, map), Replace(d.Right, map)),
        Pow p => new Pow(Replace(p.Base, map), Replace(p.Exponent, map)),
        Call c => new Call(c.Name, c.Args.Select(x => Replace(x, map)).ToArray()),
        _ => e
    };

    private static string? Command(string line, params string[] words)
    {
        foreach (var w in words)
        {
            if (line.Length <= w.Length || !line.StartsWith(w, StringComparison.OrdinalIgnoreCase)) continue;
            var rest = line[w.Length..];
            if (char.IsLetterOrDigit(rest[0]) && w is not ("d/dx" or "∫")) continue;
            rest = rest.TrimStart();
            if (rest.StartsWith("de ", StringComparison.OrdinalIgnoreCase)) rest = rest[3..];
            else if (rest.StartsWith("of ", StringComparison.OrdinalIgnoreCase)) rest = rest[3..];
            return rest.TrimStart(':', ' ');
        }
        return null;
    }

    /// <summary>A value as the person reads it: in the unit it was written in (or the SI unit of its dimension).</summary>
    public static string Show(MathValue value)
    {
        switch (value)
        {
            case Scalar s when s.Pure: return MathFormat.Number(s.Value);
            case Scalar s when s.Unit is { } u: return MathFormat.Number((s.Value - u.Offset) / u.Factor) + " " + u.Symbol;
            case Scalar s: return MathFormat.Number(s.Value) + " " + MathUnits.Named(s.Dim);
            case Vector v when v.AsMatrix() is { } m:
                return "[" + string.Join("; ", Enumerable.Range(0, m.GetLength(0)).Select(i => "[" + string.Join("; ", Enumerable.Range(0, m.GetLength(1)).Select(j => MathFormat.Number(m[i, j]))) + "]")) + "]";
            case Vector v: return "[" + string.Join("; ", v.Items.Select(Show)) + "]";
        }
        return "?";
    }

    private static string Convert(MathValue value, MathUnit target)
    {
        if (value is not Scalar s) throw new MathError("Só números com unidade se convertem.");
        if (s.Dim != target.Dim)
            throw new MathError("Não dá para converter " + (s.Pure ? "um número sem unidade" : MathUnits.Named(s.Dim)) + " em " + target.Symbol + ": dimensões diferentes.");
        return MathFormat.Number((s.Value - target.Offset) / target.Factor) + " " + target.Symbol;
    }

    [GeneratedRegex(@"^([A-Za-z_]\w*)\s*\(\s*([A-Za-z_]\w*(?:\s*[;,]\s*[A-Za-z_]\w*)*)\s*\)\s*=(?!=)\s*(.+)$")]
    private static partial Regex FunctionDefinition();
    [GeneratedRegex(@"^([A-Za-z_]\w*)\s*=(?!=)\s*(.+)$")]
    private static partial Regex Assignment();
    [GeneratedRegex(@"^(.+?)\s+(?:em|to|in|para|->|→)\s+([^\s].*)$", RegexOptions.IgnoreCase)]
    private static partial Regex Conversion();
    [GeneratedRegex(@"^(.+?)\s+(?:de|from)\s+(.+?)\s+(?:até|ate|a|to)\s+(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex DefiniteBounds();
    [GeneratedRegex(@"^(.+?)\s+(?:para|for|em relação a)\s+([A-Za-z_]\w*)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ForVariable();
}
