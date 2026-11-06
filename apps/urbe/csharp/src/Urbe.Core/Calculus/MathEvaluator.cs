namespace Urbe.Core.Calculus;

/// <summary>A computed value: a number (with a physical dimension, none for a pure number) or a list/matrix.</summary>
public abstract record MathValue;
/// <summary>A number in SI base units with its dimension; <see cref="Unit"/> is how the person wrote it, to show it back.</summary>
public sealed record Scalar(double Value, Dim Dim, MathUnit? Unit = null) : MathValue
{
    public static Scalar Of(double v) => new(v, Dim.None);
    public bool Pure => Dim.IsNone;
}
public sealed record Vector(IReadOnlyList<MathValue> Items) : MathValue
{
    public bool Equals(Vector? other) => other is not null && Items.SequenceEqual(other.Items);
    public override int GetHashCode() => Items.Count;

    /// <summary>The values as a numeric matrix when this is a list of equal-length lists of pure numbers.</summary>
    public double[,]? AsMatrix()
    {
        if (Items.Count == 0 || Items.Any(i => i is not Vector)) return null;
        var rows = Items.Cast<Vector>().ToList();
        var cols = rows[0].Items.Count;
        if (cols == 0 || rows.Any(r => r.Items.Count != cols || r.Items.Any(x => x is not Scalar { Pure: true }))) return null;
        var m = new double[rows.Count, cols];
        for (int i = 0; i < rows.Count; i++) for (int j = 0; j < cols; j++) m[i, j] = ((Scalar)rows[i].Items[j]).Value;
        return m;
    }

    public double[]? AsNumbers() => Items.All(i => i is Scalar { Pure: true }) ? Items.Cast<Scalar>().Select(s => s.Value).ToArray() : null;

    public static Vector FromMatrix(double[,] m) => new(Enumerable.Range(0, m.GetLength(0))
        .Select(i => (MathValue)new Vector(Enumerable.Range(0, m.GetLength(1)).Select(j => (MathValue)Scalar.Of(m[i, j])).ToArray())).ToArray());
}

/// <summary>A function the note defined: <c>f(x) = x^2 + 1</c>.</summary>
public sealed record UserFunction(string Name, IReadOnlyList<string> Parameters, MathExpr Body);

/// <summary>
/// Evaluates an expression with the variables of a note (<c>a = 3</c> lines before it). A name that is not a
/// variable but is a unit (km, h, °C) makes a quantity; anything else unknown is an error that names it.
/// </summary>
public static class MathEvaluator
{
    /// <summary>Functions the note defined (<c>f(x) = x^2 + 1</c>): parameter names and body.</summary>
    [ThreadStatic] private static IReadOnlyDictionary<string, UserFunction>? _functions;
    [ThreadStatic] private static int _depth;

    public static MathValue Evaluate(MathExpr e, IReadOnlyDictionary<string, MathValue>? vars, IReadOnlyDictionary<string, UserFunction>? functions)
    {
        var previous = _functions;
        _functions = functions;
        try { return Evaluate(e, vars); }
        finally { _functions = previous; }
    }

    public static MathValue Evaluate(MathExpr e, IReadOnlyDictionary<string, MathValue>? vars = null)
    {
        vars ??= new Dictionary<string, MathValue>();
        return e switch
        {
            Num n => Scalar.Of(n.Value),
            Var v => Variable(v.Name, vars),
            Neg n => Negate(Evaluate(n.Arg, vars)),
            Add a => Combine(Evaluate(a.Left, vars), Evaluate(a.Right, vars), '+'),
            Sub s => Combine(Evaluate(s.Left, vars), Evaluate(s.Right, vars), '-'),
            Mul m => Combine(Evaluate(m.Left, vars), Evaluate(m.Right, vars), '*'),
            Div d => Combine(Evaluate(d.Left, vars), Evaluate(d.Right, vars), '/'),
            Pow p => Power(Evaluate(p.Base, vars), Evaluate(p.Exponent, vars)),
            Call c when _functions is not null && _functions.TryGetValue(c.Name, out var fn) => CallUser(fn, c, vars),
            Call c => MathFunctions.Apply(c.Name, c.Args.Select(a => Evaluate(a, vars)).ToArray()),
            ListExpr l => new Vector(l.Items.Select(i => Evaluate(i, vars)).ToArray()),
            Equation => throw new MathError("Uma equação se resolve: use “resolver …”."),
            _ => throw new MathError("Expressão desconhecida.")
        };
    }

    /// <summary>A pure number from an expression of one variable (for graphs, roots, integrals).</summary>
    public static double Number(MathExpr e, string variable, double x, IReadOnlyDictionary<string, MathValue>? vars = null)
    {
        var scope = vars is null ? new Dictionary<string, MathValue>() : new Dictionary<string, MathValue>(vars);
        scope[variable] = Scalar.Of(x);
        return Evaluate(e, scope) is Scalar { Pure: true } s ? s.Value : throw new MathError("O resultado não é um número puro.");
    }

    private static MathValue CallUser(UserFunction fn, Call call, IReadOnlyDictionary<string, MathValue> vars)
    {
        if (call.Args.Count != fn.Parameters.Count) throw new MathError(fn.Name + " recebe " + fn.Parameters.Count + " valor(es).");
        if (++_depth > 64) { _depth = 0; throw new MathError(fn.Name + " chama a si mesma sem fim."); }
        try
        {
            var scope = new Dictionary<string, MathValue>(vars);
            for (int i = 0; i < fn.Parameters.Count; i++) scope[fn.Parameters[i]] = Evaluate(call.Args[i], vars);
            return Evaluate(fn.Body, scope);
        }
        finally { _depth--; }
    }

    private static MathValue Variable(string name, IReadOnlyDictionary<string, MathValue> vars)
    {
        if (vars.TryGetValue(name, out var v)) return v;
        switch (name)
        {
            case "pi": return Scalar.Of(Math.PI);
            case "e": return Scalar.Of(Math.E);
            case "inf": return Scalar.Of(double.PositiveInfinity);
            case "tau": return Scalar.Of(2 * Math.PI);
            case "phi": return Scalar.Of((1 + Math.Sqrt(5)) / 2);
        }
        if (MathUnits.TryGet(name, out var unit)) return new Scalar(unit.Factor, unit.Dim, unit);
        throw new MathError("“" + name + "” não tem valor. Defina antes, por exemplo: " + name + " = 2");
    }

    private static MathValue Negate(MathValue v) => v switch
    {
        Scalar s => s with { Value = -s.Value, Unit = s.Unit?.Offset == 0 ? s.Unit : null },
        Vector l => new Vector(l.Items.Select(Negate).ToArray()),
        _ => v
    };

    internal static MathValue Combine(MathValue a, MathValue b, char op)
    {
        if (a is Vector va && b is Vector vb)
        {
            if (op == '*' && va.AsMatrix() is { } ma && vb.AsMatrix() is { } mb) return Vector.FromMatrix(MathLinear.Multiply(ma, mb));
            if (op == '*' && va.AsMatrix() is { } m2 && vb.AsNumbers() is { } col) return new Vector(MathLinear.Apply(m2, col).Select(x => (MathValue)Scalar.Of(x)).ToArray());
            if (va.Items.Count != vb.Items.Count) throw new MathError("As listas têm tamanhos diferentes (" + va.Items.Count + " e " + vb.Items.Count + ").");
            return new Vector(va.Items.Zip(vb.Items, (x, y) => Combine(x, y, op)).ToArray());
        }
        if (a is Vector l1) return new Vector(l1.Items.Select(x => Combine(x, b, op)).ToArray());
        if (b is Vector l2) return new Vector(l2.Items.Select(x => Combine(a, x, op)).ToArray());
        var x1 = (Scalar)a; var y1 = (Scalar)b;
        // a temperature in °C or °F is a point on a scale: 20 °C + 5 is 25 °C, never 293.15 K + 5
        if (op is '+' or '-' && x1.Unit is { Offset: not 0 } scale && y1.Pure)
            return x1 with { Value = x1.Value + (op == '+' ? y1.Value : -y1.Value) * scale.Factor };
        switch (op)
        {
            case '+':
            case '-':
                if (x1.Dim != y1.Dim)
                    throw new MathError("Não dá para somar " + Describe(x1) + " com " + Describe(y1) + ": dimensões diferentes.");
                return new Scalar(op == '+' ? x1.Value + y1.Value : x1.Value - y1.Value, x1.Dim, x1.Unit?.Offset == 0 ? x1.Unit : null);
            case '*':
                // a number before a unit is a quantity (3 km); on a temperature scale it is a point (20 °C = 293,15 K)
                if (x1.Pure && y1.Unit is { } yu)
                    return yu.Offset != 0 ? new Scalar(x1.Value * yu.Factor + yu.Offset, yu.Dim, yu) : new Scalar(x1.Value * y1.Value, y1.Dim, yu);
                if (y1.Pure && x1.Unit is { } xu)
                    return xu.Offset != 0 ? new Scalar((x1.Value - xu.Offset) * y1.Value + xu.Offset, xu.Dim, xu) : new Scalar(x1.Value * y1.Value, x1.Dim, xu);
                return new Scalar(x1.Value * y1.Value, x1.Dim * y1.Dim);
            case '/':
                if (y1.Value == 0 && y1.Pure && x1.Value != 0) throw new MathError("Divisão por zero.");
                var dim = x1.Dim / y1.Dim;
                return new Scalar(x1.Value / y1.Value, dim, y1.Pure && x1.Unit is { Offset: 0 } u ? u : null);
        }
        throw new MathError("Operação desconhecida.");
    }

    private static MathValue Power(MathValue b, MathValue e)
    {
        if (e is not Scalar { Pure: true } exp) throw new MathError("O expoente precisa ser um número puro.");
        if (b is Vector v)
        {
            if (v.AsMatrix() is { } m && exp.Value == Math.Round(exp.Value))
                return Vector.FromMatrix(MathLinear.Power(m, (int)exp.Value));
            return new Vector(v.Items.Select(i => Power(i, e)).ToArray());
        }
        var s = (Scalar)b;
        if (s.Pure) return Scalar.Of(MathFunctions.RealPow(s.Value, exp.Value));
        if (exp.Value != Math.Round(exp.Value)) throw new MathError("Uma unidade só sobe a potências inteiras.");
        return new Scalar(Math.Pow(s.Value, exp.Value), s.Dim.Pow((int)exp.Value));
    }

    private static string Describe(Scalar s) => s.Pure ? "um número" : s.Unit?.Symbol ?? MathUnits.Named(s.Dim);
}
