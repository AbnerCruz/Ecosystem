using System.Xml.Linq;
using CSharpMath.Atom;
using CSharpMath.Atom.Atoms;

namespace Urbe.Core;

/// <summary>
/// A formula as presentation MathML, the equation format of OpenDocument (an embedded formula object that LibreOffice
/// edits). Same parse tree and the same rule as <see cref="MathOmml"/>: what has no faithful equivalent gives null and
/// the export keeps the TeX.
/// </summary>
public static class MathMathMl
{
    public static readonly XNamespace Ns = "http://www.w3.org/1998/Math/MathML";

    public static XElement? Convert(string tex, bool display)
    {
        if (string.IsNullOrWhiteSpace(tex) || tex.Length > MathRenderer.MaxTexLength) return null;
        var expanded = MathMacros.Expand(tex, null);
        if (!expanded.Success) return null;
        var (list, error) = LaTeXParser.MathListFromLaTeX(expanded.Expanded);
        if (error is not null || list is null) return null;
        try
        {
            // the TeX goes along as an annotation (what it was written as)
            return new XElement(Ns + "math", new XAttribute("display", display ? "block" : "inline"),
                new XElement(Ns + "semantics", Row(list), new XElement(Ns + "annotation", new XAttribute("encoding", "application/x-tex"), tex)));
        }
        catch (NotSupportedException) { return null; }
    }

    private static XElement Row(MathList list) => new(Ns + "mrow", Items(list));

    private static IEnumerable<XElement> Items(MathList list)
    {
        var atoms = list.Atoms;
        for (int i = 0; i < atoms.Count; i++)
        {
            var atom = atoms[i];
            if (atom is LargeOperator op && op.Nucleus.Length == 1 && (op.Subscript.Count > 0 || op.Superscript.Count > 0))
            {
                // limits above and below (a sum) or beside (an integral)
                var mo = new XElement(Ns + "mo", op.Nucleus);
                bool beside = op.Limits == false;
                yield return (op.Subscript.Count > 0, op.Superscript.Count > 0) switch
                {
                    (true, true) => new XElement(Ns + (beside ? "msubsup" : "munderover"), mo, Row(op.Subscript), Row(op.Superscript)),
                    (true, false) => new XElement(Ns + (beside ? "msub" : "munder"), mo, Row(op.Subscript)),
                    _ => new XElement(Ns + (beside ? "msup" : "mover"), mo, Row(op.Superscript))
                };
                continue;
            }
            if (Atom(atom) is { } e) yield return e;
        }
    }

    private static XElement? Atom(MathAtom atom)
    {
        if (Body(atom) is not { } body) return null;
        if (atom.Superscript.Count > 0 && atom.Subscript.Count > 0) return new XElement(Ns + "msubsup", body, Row(atom.Subscript), Row(atom.Superscript));
        if (atom.Superscript.Count > 0) return new XElement(Ns + "msup", body, Row(atom.Superscript));
        if (atom.Subscript.Count > 0) return new XElement(Ns + "msub", body, Row(atom.Subscript));
        return body;
    }

    private static XElement? Body(MathAtom atom) => atom switch
    {
        Fraction f => new XElement(Ns + "mfrac", f.HasRule ? null : new XAttribute("linethickness", "0"), Row(f.Numerator), Row(f.Denominator)),
        Radical r => r.Degree is { Count: > 0 } d ? new XElement(Ns + "mroot", Row(r.Radicand), Row(d)) : new XElement(Ns + "msqrt", Row(r.Radicand)),
        Inner inner => new XElement(Ns + "mrow", Fence(inner.LeftBoundary.Nucleus), Row(inner.InnerList), Fence(inner.RightBoundary.Nucleus)),
        Accent a => new XElement(Ns + "mover", new XAttribute("accent", "true"), Row(a.InnerList), new XElement(Ns + "mo", a.Nucleus)),
        Overline o => new XElement(Ns + "mover", new XAttribute("accent", "true"), Row(o.InnerList), new XElement(Ns + "mo", "‾")),
        Underline u => new XElement(Ns + "munder", Row(u.InnerList), new XElement(Ns + "mo", "_")),
        Colored c => Row(c.InnerList),
        Space or Style or Comment => null,
        Table t when t.Environment is "matrix" or "pmatrix" or "bmatrix" or "vmatrix" or "Bmatrix" => Matrix(t),
        Number => new XElement(Ns + "mn", atom.Nucleus),
        Variable => new XElement(Ns + "mi", atom.Nucleus),
        // a function name stands upright
        LargeOperator op when op.Nucleus.Length > 1 => new XElement(Ns + "mi", new XAttribute("mathvariant", "normal"), op.Nucleus),
        Ordinary when atom.Nucleus.Length == 1 && char.IsLetter(atom.Nucleus[0]) => new XElement(Ns + "mi", atom.Nucleus),
        LargeOperator or Ordinary or BinaryOperator or UnaryOperator or Relation or Punctuation or Open or Close => new XElement(Ns + "mo", atom.Nucleus),
        _ => throw new NotSupportedException(atom.GetType().Name)
    };

    private static XElement? Fence(string? c) => string.IsNullOrEmpty(c) ? null : new XElement(Ns + "mo", new XAttribute("fence", "true"), new XAttribute("stretchy", "true"), c);

    private static XElement Matrix(Table t)
    {
        var grid = new XElement(Ns + "mtable", t.Cells.Select(row => new XElement(Ns + "mtr", row.Select(cell => new XElement(Ns + "mtd", Row(cell))))));
        var (open, close) = t.Environment switch { "pmatrix" => ("(", ")"), "bmatrix" => ("[", "]"), "vmatrix" => ("|", "|"), "Bmatrix" => ("{", "}"), _ => ("", "") };
        return open.Length == 0 ? grid : new XElement(Ns + "mrow", Fence(open), grid, Fence(close));
    }
}
