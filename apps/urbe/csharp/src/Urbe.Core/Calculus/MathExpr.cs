using System.Globalization;
using System.Text;

namespace Urbe.Core.Calculus;

/// <summary>
/// CST-206: an expression of the Urbe's calculator — numbers, variables, + − × ÷ ^, functions, lists and matrices.
/// Immutable trees; evaluation, derivation and simplification are pure functions over them (nothing is ever run).
/// </summary>
public abstract record MathExpr
{
    public static MathExpr N(double v) => new Num(v);
    public static readonly MathExpr Zero = new Num(0), One = new Num(1), Two = new Num(2);

    public sealed override string ToString() => MathFormat.Write(this);
}

public sealed record Num(double Value) : MathExpr;
public sealed record Var(string Name) : MathExpr;
public sealed record Neg(MathExpr Arg) : MathExpr;
public sealed record Add(MathExpr Left, MathExpr Right) : MathExpr;
public sealed record Sub(MathExpr Left, MathExpr Right) : MathExpr;
public sealed record Mul(MathExpr Left, MathExpr Right) : MathExpr;
public sealed record Div(MathExpr Left, MathExpr Right) : MathExpr;
public sealed record Pow(MathExpr Base, MathExpr Exponent) : MathExpr;
public sealed record Call(string Name, IReadOnlyList<MathExpr> Args) : MathExpr
{
    public bool Equals(Call? other) => other is not null && Name == other.Name && Args.SequenceEqual(other.Args);
    public override int GetHashCode() => HashCode.Combine(Name, Args.Count);
}
/// <summary>A list <c>[1, 2, 3]</c>; a list of lists of the same length is a matrix.</summary>
public sealed record ListExpr(IReadOnlyList<MathExpr> Items) : MathExpr
{
    public bool Equals(ListExpr? other) => other is not null && Items.SequenceEqual(other.Items);
    public override int GetHashCode() => Items.Count;
}
/// <summary>An equation <c>left = right</c> (to solve).</summary>
public sealed record Equation(MathExpr Left, MathExpr Right) : MathExpr;

/// <summary>A wrong expression or a value outside a function's domain, told in words the person can act on.</summary>
public sealed class MathError(string message) : Exception(message);

/// <summary>Writes expressions back as the person would type them (decimal comma, as in the note).</summary>
public static class MathFormat
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>A number with up to 12 significant digits, decimal comma; integers stay integers.</summary>
    public static string Number(double v)
    {
        if (double.IsNaN(v)) return "indefinido";
        if (double.IsPositiveInfinity(v)) return "∞";
        if (double.IsNegativeInfinity(v)) return "−∞";
        if (Math.Abs(v) < 1e-300) return "0";
        var rounded = Math.Round(v);
        if (Math.Abs(v - rounded) < 1e-9 * Math.Max(1, Math.Abs(v)) && Math.Abs(rounded) < 1e15) return rounded.ToString("0", PtBr);
        var abs = Math.Abs(v);
        if (abs >= 1e15 || abs < 1e-6) return v.ToString("0.###########E+0", PtBr);
        return double.Parse(v.ToString("G12", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture).ToString("0.############", PtBr);
    }

    public static string Write(MathExpr e) => Write(e, 0);

    private static int Precedence(MathExpr e) => e switch
    {
        Equation => 0, Add or Sub => 1, Mul or Div => 2, Neg => 3, Pow => 4, Num { Value: < 0 } => 3, _ => 5
    };

    private static string Write(MathExpr e, int parent)
    {
        var p = Precedence(e);
        var s = e switch
        {
            Num n => Number(n.Value),
            Var v => v.Name,
            Neg n => "-" + Write(n.Arg, 4),
            Add a => Write(a.Left, 1) + " + " + Write(a.Right, 2),
            Sub s2 => Write(s2.Left, 1) + " - " + Write(s2.Right, 2),
            Mul m => WriteProduct(m),
            Div d => Write(d.Left, 2) + "/" + Write(d.Right, 3),
            Pow pw => Write(pw.Base, 5) + "^" + Write(pw.Exponent, 4),
            Call c => c.Name + "(" + string.Join("; ", c.Args.Select(x => Write(x, 0))) + ")",
            ListExpr l => "[" + string.Join("; ", l.Items.Select(x => Write(x, 0))) + "]",
            Equation q => Write(q.Left, 0) + " = " + Write(q.Right, 0),
            _ => "?"
        };
        return p < parent ? "(" + s + ")" : s;
    }

    /// <summary>2·x is written 2x; anything else keeps the ·.</summary>
    private static string WriteProduct(Mul m)
    {
        var left = Write(m.Left, 2);
        var right = Write(m.Right, 3);
        if (m.Left is Num { Value: >= 0 } && m.Right is Var or Pow { Base: Var } or Call) return left + right;
        return left + "·" + right;
    }
}
