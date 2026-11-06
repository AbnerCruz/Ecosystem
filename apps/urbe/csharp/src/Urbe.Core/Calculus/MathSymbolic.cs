namespace Urbe.Core.Calculus;

/// <summary>
/// Symbolic work the calculator can do exactly (CST-206): derivatives of every function it knows, simplification,
/// polynomials (expanded and collected), equations up to degree two exactly and any other numerically (told as
/// approximate), indefinite integrals of polynomials and of the elementary functions of a linear argument, definite
/// integrals numerically with an error estimate. What it cannot do it says, never guesses.
/// </summary>
public static class MathSymbolic
{
    // ------------------------------------------------------------------ derivative

    public static MathExpr Derivative(MathExpr e, string x) => Simplify(D(e, x));

    private static MathExpr D(MathExpr e, string x)
    {
        switch (e)
        {
            case Num: return MathExpr.Zero;
            case Var v: return v.Name == x ? MathExpr.One : MathExpr.Zero;
            case Neg n: return new Neg(D(n.Arg, x));
            case Add a: return new Add(D(a.Left, x), D(a.Right, x));
            case Sub s: return new Sub(D(s.Left, x), D(s.Right, x));
            case Mul m: return new Add(new Mul(D(m.Left, x), m.Right), new Mul(m.Left, D(m.Right, x)));
            case Div d: return new Div(new Sub(new Mul(D(d.Left, x), d.Right), new Mul(d.Left, D(d.Right, x))), new Pow(d.Right, MathExpr.Two));
            case Pow p:
                if (!Depends(p.Exponent, x))
                    return new Mul(new Mul(p.Exponent, new Pow(p.Base, new Sub(p.Exponent, MathExpr.One))), D(p.Base, x));
                if (!Depends(p.Base, x))
                    return new Mul(new Mul(p, new Call("ln", [p.Base])), D(p.Exponent, x));
                // u^v = e^(v ln u)
                return new Mul(p, D(new Mul(p.Exponent, new Call("ln", [p.Base])), x));
            case Call c when c.Args.Count == 1:
                var u = c.Args[0];
                var du = D(u, x);
                MathExpr outer = c.Name switch
                {
                    "sen" => new Call("cos", [u]),
                    "cos" => new Neg(new Call("sen", [u])),
                    "tan" => new Div(MathExpr.One, new Pow(new Call("cos", [u]), MathExpr.Two)),
                    "asen" => new Div(MathExpr.One, new Call("raiz", [new Sub(MathExpr.One, new Pow(u, MathExpr.Two))])),
                    "acos" => new Neg(new Div(MathExpr.One, new Call("raiz", [new Sub(MathExpr.One, new Pow(u, MathExpr.Two))]))),
                    "atan" => new Div(MathExpr.One, new Add(MathExpr.One, new Pow(u, MathExpr.Two))),
                    "senh" => new Call("cosh", [u]),
                    "cosh" => new Call("senh", [u]),
                    "tanh" => new Sub(MathExpr.One, new Pow(new Call("tanh", [u]), MathExpr.Two)),
                    "exp" => new Call("exp", [u]),
                    "ln" => new Div(MathExpr.One, u),
                    "log" => new Div(MathExpr.One, new Mul(u, new Call("ln", [MathExpr.N(10)]))),
                    "log2" => new Div(MathExpr.One, new Mul(u, new Call("ln", [MathExpr.Two]))),
                    "raiz" => new Div(MathExpr.One, new Mul(MathExpr.Two, new Call("raiz", [u]))),
                    "cbrt" => new Div(MathExpr.One, new Mul(MathExpr.N(3), new Pow(new Call("cbrt", [u]), MathExpr.Two))),
                    "abs" => new Div(u, new Call("abs", [u])),
                    _ => throw new MathError("Não sei derivar " + c.Name + "(…).")
                };
                return new Mul(outer, du);
            case Call c2: throw new MathError("Não sei derivar " + c2.Name + "(…) com vários argumentos.");
            default: throw new MathError("Não sei derivar isto.");
        }
    }

    public static bool Depends(MathExpr e, string x) => e switch
    {
        Var v => v.Name == x,
        Num => false,
        Neg n => Depends(n.Arg, x),
        Add a => Depends(a.Left, x) || Depends(a.Right, x),
        Sub s => Depends(s.Left, x) || Depends(s.Right, x),
        Mul m => Depends(m.Left, x) || Depends(m.Right, x),
        Div d => Depends(d.Left, x) || Depends(d.Right, x),
        Pow p => Depends(p.Base, x) || Depends(p.Exponent, x),
        Call c => c.Args.Any(a => Depends(a, x)),
        ListExpr l => l.Items.Any(a => Depends(a, x)),
        Equation q => Depends(q.Left, x) || Depends(q.Right, x),
        _ => false
    };

    /// <summary>The free names of an expression (variables, not constants or functions).</summary>
    public static IReadOnlyCollection<string> Variables(MathExpr e)
    {
        var set = new SortedSet<string>(StringComparer.Ordinal);
        void Walk(MathExpr x)
        {
            switch (x)
            {
                case Var v when v.Name is not ("pi" or "e" or "inf" or "tau" or "phi"): set.Add(v.Name); break;
                case Neg n: Walk(n.Arg); break;
                case Add a: Walk(a.Left); Walk(a.Right); break;
                case Sub s: Walk(s.Left); Walk(s.Right); break;
                case Mul m: Walk(m.Left); Walk(m.Right); break;
                case Div d: Walk(d.Left); Walk(d.Right); break;
                case Pow p: Walk(p.Base); Walk(p.Exponent); break;
                case Call c: foreach (var a in c.Args) Walk(a); break;
                case ListExpr l: foreach (var a in l.Items) Walk(a); break;
                case Equation q: Walk(q.Left); Walk(q.Right); break;
            }
        }
        Walk(e);
        return set;
    }

    // ------------------------------------------------------------------ simplification

    /// <summary>
    /// A tidier equivalent: numbers folded, neutral terms removed (x+0, 1·x, x^1), and a polynomial in a single
    /// variable expanded and collected (3x^2 + 2x - 1). Never changes the value of the expression.
    /// </summary>
    public static MathExpr Simplify(MathExpr e, bool expand = false)
    {
        var s = Fold(e);
        var vars = Variables(s);
        if (vars.Count == 1 && Polynomial(s, vars.First()) is { } poly && poly.Count > 0 && poly.Keys.Max() <= 12)
        {
            var collected = FromPolynomial(poly, vars.First());
            if (expand || Size(collected) <= Size(s)) return collected;
        }
        return s;
    }

    private static int Size(MathExpr e) => e.ToString().Length;

    private static MathExpr Fold(MathExpr e)
    {
        switch (e)
        {
            case Neg n:
                var a = Fold(n.Arg);
                return a switch { Num k => new Num(-k.Value), Neg nn => nn.Arg, _ => new Neg(a) };
            case Add add:
            {
                var l = Fold(add.Left); var r = Fold(add.Right);
                if (l is Num x && r is Num y) return new Num(x.Value + y.Value);
                if (l is Num { Value: 0 }) return r;
                if (r is Num { Value: 0 }) return l;
                if (r is Neg rn) return Fold(new Sub(l, rn.Arg));
                if (r is Num { Value: < 0 } rneg) return new Sub(l, new Num(-rneg.Value));
                if (l == r) return Fold(new Mul(MathExpr.Two, l));
                return new Add(l, r);
            }
            case Sub sub:
            {
                var l = Fold(sub.Left); var r = Fold(sub.Right);
                if (l is Num x && r is Num y) return new Num(x.Value - y.Value);
                if (r is Num { Value: 0 }) return l;
                if (l is Num { Value: 0 }) return Fold(new Neg(r));
                if (l == r) return MathExpr.Zero;
                if (r is Neg rn) return Fold(new Add(l, rn.Arg));
                if (r is Num { Value: < 0 } rneg) return new Add(l, new Num(-rneg.Value));
                return new Sub(l, r);
            }
            case Mul mul:
            {
                var l = Fold(mul.Left); var r = Fold(mul.Right);
                if (l is Num x && r is Num y) return new Num(x.Value * y.Value);
                if (l is Num { Value: 0 } || r is Num { Value: 0 }) return MathExpr.Zero;
                if (l is Num { Value: 1 }) return r;
                if (r is Num { Value: 1 }) return l;
                if (l is Num { Value: -1 }) return Fold(new Neg(r));
                if (r is Num { Value: -1 }) return Fold(new Neg(l));
                if (r is Num && l is not Num) (l, r) = (r, l); // numbers first: 2x
                if (l is Num k1 && r is Mul { Left: Num k2 } inner) return Fold(new Mul(new Num(k1.Value * k2.Value), inner.Right));
                if (l is Neg ln) return Fold(new Neg(new Mul(ln.Arg, r)));
                if (r is Neg rn) return Fold(new Neg(new Mul(l, rn.Arg)));
                if (l == r) return new Pow(l, MathExpr.Two);
                return new Mul(l, r);
            }
            case Div div:
            {
                var l = Fold(div.Left); var r = Fold(div.Right);
                if (l is Num x && r is Num { Value: not 0 } y) return new Num(x.Value / y.Value);
                if (l is Num { Value: 0 } && r is not Num { Value: 0 }) return MathExpr.Zero;
                if (r is Num { Value: 1 }) return l;
                if (l == r) return MathExpr.One;
                return new Div(l, r);
            }
            case Pow pow:
            {
                var b = Fold(pow.Base); var x = Fold(pow.Exponent);
                if (b is Num nb && x is Num nx) { try { return new Num(MathFunctions.RealPow(nb.Value, nx.Value)); } catch (MathError) { return new Pow(b, x); } }
                if (x is Num { Value: 0 }) return MathExpr.One;
                if (x is Num { Value: 1 }) return b;
                if (b is Pow inner && inner.Exponent is Num e1 && x is Num e2) return Fold(new Pow(inner.Base, new Num(e1.Value * e2.Value)));
                return new Pow(b, x);
            }
            case Call c:
            {
                var args = c.Args.Select(Fold).ToArray();
                if (args.All(a => a is Num) && c.Name is not ("ln" or "log" or "raiz") || args.All(a => a is Num) && Exact(c.Name, args))
                {
                    try { if (MathFunctions.Apply(c.Name, args.Select(a => (MathValue)Scalar.Of(((Num)a).Value)).ToArray()) is Scalar s && IsNice(s.Value)) return new Num(s.Value); }
                    catch (MathError) { }
                }
                if (c.Name == "ln" && args is [Call { Name: "exp" } inner]) return inner.Args[0];
                if (c.Name == "exp" && args is [Call { Name: "ln" } inner2]) return inner2.Args[0];
                return new Call(c.Name, args);
            }
            case ListExpr l: return new ListExpr(l.Items.Select(Fold).ToArray());
            case Equation q: return new Equation(Fold(q.Left), Fold(q.Right));
            default: return e;
        }
    }

    /// <summary>ln(1), raiz(9): folded only when the answer is a round number (√2 stays √2 in an exact result).</summary>
    private static bool Exact(string name, MathExpr[] args)
    {
        try
        {
            var v = ((Scalar)MathFunctions.Apply(name, args.Select(a => (MathValue)Scalar.Of(((Num)a).Value)).ToArray())).Value;
            return Math.Abs(v - Math.Round(v)) < 1e-12;
        }
        catch (MathError) { return false; }
    }

    private static bool IsNice(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

    // ------------------------------------------------------------------ polynomials

    /// <summary>The coefficients by power when <paramref name="e"/> is a polynomial in <paramref name="x"/> with numeric coefficients.</summary>
    public static SortedDictionary<int, double>? Polynomial(MathExpr e, string x)
    {
        static SortedDictionary<int, double> Single(int p, double c) => new() { [p] = c };
        static SortedDictionary<int, double> Plus(SortedDictionary<int, double> a, SortedDictionary<int, double> b, double sign)
        {
            var r = new SortedDictionary<int, double>(a);
            foreach (var (k, v) in b) r[k] = r.GetValueOrDefault(k) + sign * v;
            return r;
        }
        static SortedDictionary<int, double> Times(SortedDictionary<int, double> a, SortedDictionary<int, double> b)
        {
            var r = new SortedDictionary<int, double>();
            foreach (var (i, u) in a) foreach (var (j, v) in b) r[i + j] = r.GetValueOrDefault(i + j) + u * v;
            return r;
        }
        switch (e)
        {
            case Num n: return Single(0, n.Value);
            case Var v when v.Name == x: return Single(1, 1);
            case Var v2 when v2.Name is "pi": return Single(0, Math.PI);
            case Var v3 when v3.Name is "e": return Single(0, Math.E);
            case Var: return null;
            case Neg ng: return Polynomial(ng.Arg, x) is { } p ? Plus(new(), p, -1) : null;
            case Add a: return Polynomial(a.Left, x) is { } l && Polynomial(a.Right, x) is { } r ? Plus(l, r, 1) : null;
            case Sub s: return Polynomial(s.Left, x) is { } l2 && Polynomial(s.Right, x) is { } r2 ? Plus(l2, r2, -1) : null;
            case Mul m: return Polynomial(m.Left, x) is { } l3 && Polynomial(m.Right, x) is { } r3 ? Times(l3, r3) : null;
            case Div d:
                if (Polynomial(d.Right, x) is { Count: 1 } den && den.ContainsKey(0) && den[0] != 0 && Polynomial(d.Left, x) is { } num)
                    return new SortedDictionary<int, double>(num.ToDictionary(k => k.Key, k => k.Value / den[0]));
                return null;
            case Pow pw when pw.Exponent is Num { Value: >= 0 and <= 12 } k && k.Value == Math.Round(k.Value) && Polynomial(pw.Base, x) is { } b:
                var result = Single(0, 1);
                for (int i = 0; i < (int)k.Value; i++) result = Times(result, b);
                return result;
            default:
                return Depends(e, x) ? null : TryConstant(e) is { } c ? Single(0, c) : null;
        }
    }

    private static double? TryConstant(MathExpr e)
    {
        try { return MathEvaluator.Evaluate(e) is Scalar { Pure: true } s ? s.Value : null; }
        catch (MathError) { return null; }
    }

    public static MathExpr FromPolynomial(IReadOnlyDictionary<int, double> poly, string x)
    {
        MathExpr? result = null;
        foreach (var (power, raw) in poly.OrderByDescending(k => k.Key))
        {
            var c = Clean(raw);
            if (c == 0) continue;
            MathExpr term = power == 0 ? new Num(Math.Abs(c)) : power == 1 ? new Var(x) : new Pow(new Var(x), new Num(power));
            if (power > 0 && Math.Abs(c) != 1) term = new Mul(new Num(Math.Abs(c)), term);
            if (result is null) result = c < 0 ? new Neg(term) : term;
            else result = c < 0 ? new Sub(result, term) : new Add(result, term);
        }
        return result ?? MathExpr.Zero;
    }

    private static double Clean(double v) => Math.Abs(v) < 1e-12 ? 0 : Math.Abs(v - Math.Round(v)) < 1e-10 ? Math.Round(v) : v;

    // ------------------------------------------------------------------ equations

    /// <summary>The solutions of an equation in <paramref name="x"/>; exact for degree ≤ 2, else numeric.</summary>
    public static SolveResult Solve(MathExpr equation, string x, IReadOnlyDictionary<string, MathValue>? vars = null)
    {
        var f = equation is Equation q ? new Sub(q.Left, q.Right) : equation;
        f = Substitute(f, vars, x);
        if (Polynomial(f, x) is { } poly)
        {
            foreach (var k in poly.Keys.ToList()) if (Math.Abs(poly[k]) < 1e-14) poly.Remove(k);
            var degree = poly.Count == 0 ? -1 : poly.Keys.Max();
            double a = poly.GetValueOrDefault(2), b = poly.GetValueOrDefault(1), c = poly.GetValueOrDefault(0);
            if (degree == -1) return new SolveResult([], true, "Toda solução vale: os dois lados são iguais.", Identity: true);
            if (degree == 0) return new SolveResult([], true, "Sem solução: a equação se reduz a " + MathFormat.Number(c) + " = 0.");
            if (degree == 1) return new SolveResult([Exact(-c / b)], true, null);
            if (degree == 2)
            {
                var delta = b * b - 4 * a * c;
                if (delta < -1e-12) return new SolveResult([], true, "Sem solução real (Δ = " + MathFormat.Number(delta) + " < 0).", Delta: delta);
                if (Math.Abs(delta) < 1e-12) return new SolveResult([Exact(-b / (2 * a))], true, null, Delta: 0);
                var sq = Math.Sqrt(delta);
                var roots = new[] { (-b - sq) / (2 * a), (-b + sq) / (2 * a) }.Order().Select(Exact).ToArray();
                // √Δ not whole: also the exact form (−b ± √Δ)/2a
                string? exactForm = Math.Abs(sq - Math.Round(sq)) > 1e-9
                    ? Math.Abs(b) < 1e-14 ? x + " = ±√" + MathFormat.Number(-c / a)
                    : x + " = (" + MathFormat.Number(-b) + " ± √" + MathFormat.Number(delta) + ")/" + MathFormat.Number(2 * a) : null;
                return new SolveResult(roots, true, null, delta, exactForm);
            }
        }
        // numeric: sign changes and touching minima over a wide range, refined
        var found = NumericRoots(t => MathEvaluator.Number(f, x, t), -1000, 1000);
        // a numeric root that substitutes back to exactly zero is exact (2^t = 8 → t = 3)
        bool Checks(double r) { try { return r == Math.Round(r) && MathEvaluator.Number(f, x, r) == 0; } catch (MathError) { return false; } }
        return new SolveResult(found.Select(r => new SolveRoot(r, Checks(r))).ToArray(), found.All(Checks),
            found.Count == 0 ? "Nenhuma solução real encontrada entre -1000 e 1000." : null);
    }

    private static SolveRoot Exact(double v) => new(Clean(v), true);

    /// <summary>The note's numeric variables put in, so <c>a·x + b = 0</c> with a and b defined is solved for x.</summary>
    public static MathExpr Substitute(MathExpr e, IReadOnlyDictionary<string, MathValue>? vars, string keep)
    {
        if (vars is null || vars.Count == 0) return e;
        return e switch
        {
            Var v when v.Name != keep && vars.TryGetValue(v.Name, out var val) && val is Scalar { Pure: true } s => new Num(s.Value),
            Neg n => new Neg(Substitute(n.Arg, vars, keep)),
            Add a => new Add(Substitute(a.Left, vars, keep), Substitute(a.Right, vars, keep)),
            Sub s2 => new Sub(Substitute(s2.Left, vars, keep), Substitute(s2.Right, vars, keep)),
            Mul m => new Mul(Substitute(m.Left, vars, keep), Substitute(m.Right, vars, keep)),
            Div d => new Div(Substitute(d.Left, vars, keep), Substitute(d.Right, vars, keep)),
            Pow p => new Pow(Substitute(p.Base, vars, keep), Substitute(p.Exponent, vars, keep)),
            Call c => new Call(c.Name, c.Args.Select(a => Substitute(a, vars, keep)).ToArray()),
            Equation q => new Equation(Substitute(q.Left, vars, keep), Substitute(q.Right, vars, keep)),
            _ => e
        };
    }

    public static List<double> NumericRoots(Func<double, double> f, double from, double to, int steps = 20000)
    {
        var roots = new List<double>();
        double Safe(double t) { try { return f(t); } catch (MathError) { return double.NaN; } }
        double h = (to - from) / steps, x0 = from, y0 = Safe(from);
        for (int i = 1; i <= steps; i++)
        {
            double x1 = from + i * h, y1 = Safe(x1);
            if (!double.IsNaN(y0) && !double.IsNaN(y1) && double.IsFinite(y0) && double.IsFinite(y1))
            {
                if (y0 == 0) Add(x0);
                else if (Math.Sign(y0) != Math.Sign(y1) && y1 != 0)
                {
                    // bisection, then reject a pole (the value blows up instead of shrinking)
                    double a = x0, b = x1, fa = y0;
                    for (int k = 0; k < 200; k++)
                    {
                        var m = (a + b) / 2; var fm = Safe(m);
                        if (double.IsNaN(fm)) break;
                        if (Math.Sign(fm) == Math.Sign(fa)) { a = m; fa = fm; } else b = m;
                    }
                    var r = (a + b) / 2;
                    if (Math.Abs(Safe(r)) < 1e-6 * Math.Max(1, Math.Max(Math.Abs(y0), Math.Abs(y1)))) Add(r);
                }
            }
            x0 = x1; y0 = y1;
        }
        return roots;

        void Add(double r)
        {
            r = Math.Abs(r - Math.Round(r)) < 1e-9 ? Math.Round(r) : r;
            if (roots.All(o => Math.Abs(o - r) > 1e-7)) roots.Add(r);
        }
    }

    // ------------------------------------------------------------------ integrals

    /// <summary>An antiderivative when one is known (polynomials; elementary functions of a·x + b; sums and constant multiples).</summary>
    public static MathExpr? Antiderivative(MathExpr e, string x)
    {
        var f = Fold(e);
        if (!Depends(f, x)) return Fold(new Mul(f, new Var(x)));
        if (Polynomial(f, x) is { } poly)
            return FromPolynomial(poly.ToDictionary(k => k.Key + 1, k => k.Value / (k.Key + 1)), x);
        switch (f)
        {
            case Add a: return Antiderivative(a.Left, x) is { } l && Antiderivative(a.Right, x) is { } r ? Fold(new Add(l, r)) : null;
            case Sub s: return Antiderivative(s.Left, x) is { } l2 && Antiderivative(s.Right, x) is { } r2 ? Fold(new Sub(l2, r2)) : null;
            case Neg n: return Antiderivative(n.Arg, x) is { } i ? Fold(new Neg(i)) : null;
            case Mul m when !Depends(m.Left, x): return Antiderivative(m.Right, x) is { } i2 ? Fold(new Mul(m.Left, i2)) : null;
            case Mul m2 when !Depends(m2.Right, x): return Antiderivative(m2.Left, x) is { } i3 ? Fold(new Mul(m2.Right, i3)) : null;
            case Div d when !Depends(d.Right, x): return Antiderivative(d.Left, x) is { } i4 ? Fold(new Div(i4, d.Right)) : null;
            case Div d2 when !Depends(d2.Left, x) && Linear(d2.Right, x) is { } lin:
                // k/(a x + b) → (k/a) ln|a x + b|
                return Fold(new Mul(new Div(d2.Left, new Num(lin.A)), new Call("ln", [new Call("abs", [d2.Right])])));
            case Pow p when !Depends(p.Exponent, x) && Linear(p.Base, x) is { } lin2:
                if (TryConstant(p.Exponent) is { } k && Math.Abs(k + 1) < 1e-12)
                    return Fold(new Div(new Call("ln", [new Call("abs", [p.Base])]), new Num(lin2.A)));
                return Fold(new Div(new Pow(p.Base, new Add(p.Exponent, MathExpr.One)), new Mul(new Add(p.Exponent, MathExpr.One), new Num(lin2.A))));
            case Pow p2 when !Depends(p2.Base, x) && Linear(p2.Exponent, x) is { } lin3:
                // c^(a x + b) → c^(a x + b) / (a ln c)
                return Fold(new Div(p2, new Mul(new Num(lin3.A), new Call("ln", [p2.Base]))));
            case Call c when c.Args.Count == 1 && Linear(c.Args[0], x) is { } lin4:
                var u = c.Args[0];
                MathExpr? g = c.Name switch
                {
                    "sen" => new Neg(new Call("cos", [u])),
                    "cos" => new Call("sen", [u]),
                    "exp" => new Call("exp", [u]),
                    "senh" => new Call("cosh", [u]),
                    "cosh" => new Call("senh", [u]),
                    "tan" => new Neg(new Call("ln", [new Call("abs", [new Call("cos", [u])])])),
                    "ln" => new Sub(new Mul(u, new Call("ln", [u])), u),
                    "raiz" => new Mul(new Div(MathExpr.Two, MathExpr.N(3)), new Pow(u, new Num(1.5))),
                    _ => null
                };
                return g is null ? null : Fold(new Div(g, new Num(lin4.A)));
        }
        return null;
    }

    /// <summary>a and b when <paramref name="e"/> is a·x + b with a ≠ 0.</summary>
    private static (double A, double B)? Linear(MathExpr e, string x) =>
        Polynomial(e, x) is { } p && p.Keys.All(k => k <= 1) && p.GetValueOrDefault(1) != 0 ? (p[1], p.GetValueOrDefault(0)) : null;

    /// <summary>∫ from a to b, adaptive Simpson; the error estimate comes with it.</summary>
    public static (double Value, double Error) Integrate(Func<double, double> f, double a, double b, double tolerance = 1e-10)
    {
        if (double.IsInfinity(a) || double.IsInfinity(b)) throw new MathError("Integral com limite infinito ainda não é calculada.");
        double fa = f(a), fb = f(b), m = (a + b) / 2, fm = f(m);
        if (!double.IsFinite(fa) || !double.IsFinite(fb) || !double.IsFinite(fm)) throw new MathError("A função não é finita no intervalo de integração.");
        var whole = (b - a) / 6 * (fa + 4 * fm + fb);
        double error = 0;
        var value = Step(a, b, fa, fm, fb, whole, tolerance, 50);
        return (value, Math.Max(error, tolerance));

        double Step(double l, double r, double fl, double fmid, double fr, double s, double eps, int depth)
        {
            double lm = (l + (l + r) / 2) / 2, rm = ((l + r) / 2 + r) / 2, flm = f(lm), frm = f(rm), mid = (l + r) / 2;
            if (!double.IsFinite(flm) || !double.IsFinite(frm)) throw new MathError("A função não é finita no intervalo de integração.");
            double left = (mid - l) / 6 * (fl + 4 * flm + fmid), right = (r - mid) / 6 * (fmid + 4 * frm + fr), diff = left + right - s;
            if (depth <= 0 || Math.Abs(diff) <= 15 * eps)
            {
                error += Math.Abs(diff) / 15;
                return left + right + diff / 15;
            }
            return Step(l, mid, fl, flm, fmid, left, eps / 2, depth - 1) + Step(mid, r, fmid, frm, fr, right, eps / 2, depth - 1);
        }
    }
}

/// <summary>A root, exact when it came from a formula, approximate when found numerically.</summary>
public sealed record SolveRoot(double Value, bool Exact);

/// <summary>The solutions of an equation and how they were obtained.</summary>
public sealed record SolveResult(IReadOnlyList<SolveRoot> Roots, bool ExactMethod, string? Note, double? Delta = null, string? ExactForm = null, bool Identity = false);
