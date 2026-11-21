using System.Xml.Linq;
using CSharpMath.Atom;
using CSharpMath.Atom.Atoms;

namespace Urbe.Core;

/// <summary>
/// A formula as a native Word equation (Office Math Markup, ECMA-376 part 1 §22.1), built from the parse tree CSharpMath
/// already makes for drawing it: runs, fractions, roots, scripts, big operators, delimiters, accents, bars and matrices.
/// Anything else gives null and the export keeps the TeX as editable text, as before (never a wrong equation).
/// </summary>
public static class MathOmml
{
    public static readonly XNamespace M = "http://schemas.openxmlformats.org/officeDocument/2006/math";

    /// <summary>&lt;m:oMath&gt; for <paramref name="tex"/>, or null when some part has no faithful equivalent here.</summary>
    public static XElement? Convert(string tex)
    {
        if (string.IsNullOrWhiteSpace(tex) || tex.Length > MathRenderer.MaxTexLength) return null;
        var expanded = MathMacros.Expand(tex, null);
        if (!expanded.Success) return null;
        var (list, error) = LaTeXParser.MathListFromLaTeX(expanded.Expanded);
        if (error is not null || list is null) return null;
        try { return new XElement(M + "oMath", List(list)); }
        catch (NotSupportedException) { return null; }
    }

    private static IEnumerable<XElement> List(MathList list)
    {
        var atoms = list.Atoms;
        for (int i = 0; i < atoms.Count; i++)
        {
            var atom = atoms[i];
            if (atom is LargeOperator op && op.Nucleus is "∑" or "∏" or "∐" or "∫" or "∬" or "∭" or "∮" or "⋃" or "⋂")
            {
                // an n-ary operator applies to what follows it, up to the next relation or + / −
                var operand = new List<XElement>();
                while (i + 1 < atoms.Count && atoms[i + 1] is not (Relation or BinaryOperator)) operand.AddRange(Atom(atoms[++i]));
                yield return new XElement(M + "nary",
                    new XElement(M + "naryPr", new XElement(M + "chr", Val(op.Nucleus)),
                        // limits beside (an integral) or above and below (a sum in display)
                        op.Limits == false ? new XElement(M + "limLoc", Val("subSup")) : null,
                        op.Subscript.Count == 0 ? new XElement(M + "subHide", Val("1")) : null,
                        op.Superscript.Count == 0 ? new XElement(M + "supHide", Val("1")) : null),
                    new XElement(M + "sub", List(op.Subscript)), new XElement(M + "sup", List(op.Superscript)), new XElement(M + "e", operand));
                continue;
            }
            foreach (var e in Atom(atom)) yield return e;
        }
    }

    private static IEnumerable<XElement> Atom(MathAtom atom)
    {
        var body = Body(atom);
        if (atom.Superscript.Count == 0 && atom.Subscript.Count == 0) return body;
        var e = new XElement(M + "e", body);
        if (atom.Superscript.Count > 0 && atom.Subscript.Count > 0)
            return [new XElement(M + "sSubSup", e, new XElement(M + "sub", List(atom.Subscript)), new XElement(M + "sup", List(atom.Superscript)))];
        return atom.Superscript.Count > 0
            ? [new XElement(M + "sSup", e, new XElement(M + "sup", List(atom.Superscript)))]
            : [new XElement(M + "sSub", e, new XElement(M + "sub", List(atom.Subscript)))];
    }

    private static IEnumerable<XElement> Body(MathAtom atom) => atom switch
    {
        Fraction f => [new XElement(M + "f", f.HasRule ? null : new XElement(M + "fPr", new XElement(M + "type", Val("noBar"))),
            new XElement(M + "num", List(f.Numerator)), new XElement(M + "den", List(f.Denominator)))],
        Radical r => [new XElement(M + "rad", r.Degree is { Count: > 0 } ? null : new XElement(M + "radPr", new XElement(M + "degHide", Val("1"))),
            new XElement(M + "deg", r.Degree is { } d ? List(d) : null), new XElement(M + "e", List(r.Radicand)))],
        Inner inner => [new XElement(M + "d", new XElement(M + "dPr", new XElement(M + "begChr", Val(inner.LeftBoundary.Nucleus ?? "")), new XElement(M + "endChr", Val(inner.RightBoundary.Nucleus ?? ""))),
            new XElement(M + "e", List(inner.InnerList)))],
        Accent a => [new XElement(M + "acc", new XElement(M + "accPr", new XElement(M + "chr", Val(a.Nucleus))), new XElement(M + "e", List(a.InnerList)))],
        Overline o => [new XElement(M + "bar", new XElement(M + "barPr", new XElement(M + "pos", Val("top"))), new XElement(M + "e", List(o.InnerList)))],
        Underline u => [new XElement(M + "bar", new XElement(M + "barPr", new XElement(M + "pos", Val("bot"))), new XElement(M + "e", List(u.InnerList)))],
        Colored c => List(c.InnerList),
        Space or Style or Comment => [],
        Table t when t.Environment is "matrix" or "pmatrix" or "bmatrix" or "vmatrix" or "Bmatrix" => Matrix(t),
        // a function name (sin, log, lim without limits) stands upright
        LargeOperator op => [Run(op.Nucleus, upright: op.Nucleus.Length > 1)],
        Variable or Number or Ordinary or BinaryOperator or UnaryOperator or Relation or Punctuation or Open or Close => [Run(atom.Nucleus, upright: atom is not Variable)],
        _ => throw new NotSupportedException(atom.GetType().Name)
    };

    private static IEnumerable<XElement> Matrix(Table t)
    {
        var grid = new XElement(M + "m", t.Cells.Select(row => new XElement(M + "mr", row.Select(cell => new XElement(M + "e", List(cell))))));
        var (open, close) = t.Environment switch { "pmatrix" => ("(", ")"), "bmatrix" => ("[", "]"), "vmatrix" => ("|", "|"), "Bmatrix" => ("{", "}"), _ => ("", "") };
        return open.Length == 0 ? [grid]
            : [new XElement(M + "d", new XElement(M + "dPr", new XElement(M + "begChr", Val(open)), new XElement(M + "endChr", Val(close))), new XElement(M + "e", grid))];
    }

    private static XElement Run(string text, bool upright) =>
        new(M + "r", upright ? new XElement(M + "rPr", new XElement(M + "sty", Val("p"))) : null, new XElement(M + "t", text));

    private static XAttribute Val(string value) => new(M + "val", value);
}
